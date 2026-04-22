using System.Collections.Concurrent;
using JSSP.Algorithms.Constructive;
using JSSP.Core.Interfaces;
using JSSP.Core.Models;

namespace JSSP.Algorithms.Genetic;

/// <summary>
/// Genetic algorithm for JSSP makespan minimisation using job-based chromosome
/// encoding and Precedence Operation Crossover (POX), informed by the MAGATS
/// paper (PLOS ONE, 2019, doi:10.1371/journal.pone.0223182).
///
/// Encoding: integer array of length equal to the total number of operations
/// across all jobs. Gene value <c>j</c> represents job <c>j</c>; job index
/// <c>j</c> appears exactly as many times as job <c>j</c> has operations.
/// Decoding left-to-right: the k-th occurrence of job j maps to its k-th operation.
///
/// Crossover: POX (Precedence Operation Crossover) — designed for JSSP.
/// Selection: k-way tournament (lower makespan wins).
/// Mutation: single swap of two randomly chosen genes.
/// Replacement: generational with elitism (top EliteCount chromosomes survive).
///
/// When <c>config.Repetitions &gt; 1</c> and the problem is large enough
/// (≥ <see cref="ParallelRepetitionThreshold"/> total operations), independent
/// repetitions are run concurrently via <c>Parallel.ForEach</c>. Smaller instances
/// fall back to the sequential path to avoid parallelism overhead exceeding the
/// per-repetition work.
/// </summary>
public class GeneticAlgorithm : IAlgorithm
{
    /// <summary>
    /// Minimum total operation count (across all jobs) required to use the parallel
    /// repetition path. Below this threshold the sequential path is used.
    /// </summary>
    private const int ParallelRepetitionThreshold = 10;

    private AlgorithmStats? _stats;

    /// <inheritdoc/>
    public event EventHandler<ConvergenceEventArgs>? OnConvergence;

    /// <inheritdoc/>
    public Schedule Solve(List<Job> jobs, AlgorithmConfig config)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var jobIds = jobs.Select(j => j.Id).ToList();
        var jobLookup = jobs.ToDictionary(j => j.Id);
        int totalOps = jobs.Sum(j => j.Operations.Count);

        Schedule bestSchedule = null!;
        int overallBestMakespan = int.MaxValue;
        int bestConvergenceGen = 0;
        var repMakespans = new List<int>(config.Repetitions);

        bool useParallel = config.Repetitions > 1 && totalOps >= ParallelRepetitionThreshold;

        if (useParallel)
        {
            // Each parallel repetition gets its own Random and FitnessCache so that
            // there are no shared mutable state data races. Seeds are derived from a
            // master seed for diversity while remaining deterministic within a run.
            int masterSeed = new Random().Next();
            var results = new ConcurrentBag<(Schedule Schedule, int Makespan, int ConvergenceGen)>();

            var parallelOpts = new ParallelOptions
            {
                CancellationToken = config.CancellationToken
            };

            try
            {
                Parallel.ForEach(Enumerable.Range(0, config.Repetitions), parallelOpts, rep =>
                {
                    var localRng = new Random(masterSeed + rep);
                    var localCache = new FitnessCache();
                    var result = RunOnce(jobs, jobIds, jobLookup, config, localRng, localCache);
                    results.Add(result);
                });
            }
            catch (OperationCanceledException) { /* partial results used below */ }

            foreach (var (schedule, makespan, convergenceGen) in results)
            {
                repMakespans.Add(makespan);
                if (makespan < overallBestMakespan)
                {
                    overallBestMakespan = makespan;
                    bestSchedule = schedule;
                    bestConvergenceGen = convergenceGen;
                }
            }
        }
        else
        {
            // Sequential path: small datasets or single repetition.
            var rng = new Random();
            var cache = new FitnessCache();

            for (int rep = 0; rep < config.Repetitions; rep++)
            {
                if (config.CancellationToken.IsCancellationRequested)
                    break;

                var (schedule, makespan, convergenceGen) =
                    RunOnce(jobs, jobIds, jobLookup, config, rng, cache);

                repMakespans.Add(makespan);

                if (makespan < overallBestMakespan)
                {
                    overallBestMakespan = makespan;
                    bestSchedule = schedule;
                    bestConvergenceGen = convergenceGen;
                }
            }
        }

        // Guard: if cancelled before any rep completed, run one random decode as fallback.
        if (repMakespans.Count == 0)
        {
            var fallbackRng = new Random();
            var fallback = Chromosome.CreateRandom(jobs, fallbackRng);
            bestSchedule = DecodeToSchedule(fallback, jobLookup);
            overallBestMakespan = ComputeMakespan(fallback.ToArray(), jobLookup);
            repMakespans.Add(overallBestMakespan);
        }

        double mean = repMakespans.Average();
        double variance = repMakespans.Sum(m => Math.Pow(m - mean, 2)) / repMakespans.Count;

        sw.Stop();

        _stats = new AlgorithmStats
        {
            BestMakespan = overallBestMakespan,
            MeanMakespan = mean,
            StdDev = Math.Sqrt(variance),
            ConvergenceGeneration = bestConvergenceGen,
            ElapsedTime = sw.Elapsed,
        };

        return bestSchedule;
    }

    /// <inheritdoc/>
    public AlgorithmStats? GetStats() => _stats;

    // -------------------------------------------------------------------------
    // Core loop
    // -------------------------------------------------------------------------

    /// <summary>
    /// Executes a single full GA run (one of <c>config.Repetitions</c> independent runs).
    /// </summary>
    /// <returns>
    /// Tuple of (best schedule found, its makespan, generation at which it was first found).
    /// </returns>
    private (Schedule schedule, int makespan, int convergenceGen) RunOnce(
        List<Job> jobs,
        IReadOnlyList<int> jobIds,
        Dictionary<int, Job> jobLookup,
        AlgorithmConfig config,
        Random rng,
        FitnessCache cache)
    {
        // --- Initialise population ---
        // Seed slot 0 with an LRW chromosome for a high-quality starting individual;
        // remaining slots are filled with randomly shuffled chromosomes for diversity.
        var population = new List<Chromosome>(config.PopulationSize);
        population.Add(Chromosome.FromArray(LargestRemainingWorkload.Create(jobs, rng)));
        for (int i = 1; i < config.PopulationSize; i++)
            population.Add(Chromosome.CreateRandom(jobs, rng));

        // --- Evaluate initial population in parallel (no Random needed for fitness) ---
        var initialFitnesses = new int[config.PopulationSize];
        Parallel.For(0, config.PopulationSize, i =>
            initialFitnesses[i] = EvaluateFitness(population[i], jobLookup, cache));
        var fitnesslist = initialFitnesses.ToList();

        int bestMakespan = fitnesslist.Min();
        int bestIdx = fitnesslist.IndexOf(bestMakespan);
        Chromosome bestChromosome = population[bestIdx];
        int convergenceGen = 0;

        // --- Generational loop ---
        for (int gen = 1; gen <= config.Generations; gen++)
        {
            if (config.CancellationToken.IsCancellationRequested)
                break;
            var nextGen = new List<Chromosome>(config.PopulationSize);
            var nextFit = new List<int>(config.PopulationSize);

            // Elitism: carry the best EliteCount individuals unchanged into next generation.
            var eliteIndices = fitnesslist
                .Select((f, i) => (f, i))
                .OrderBy(t => t.f)
                .Take(config.EliteCount)
                .Select(t => t.i)
                .ToList();

            foreach (int ei in eliteIndices)
            {
                nextGen.Add(population[ei]);
                nextFit.Add(fitnesslist[ei]);
            }

            // Fill remaining slots with offspring from selection → crossover → mutation.
            while (nextGen.Count < config.PopulationSize)
            {
                var parent1 = TournamentSelector.Select(population, fitnesslist, config.TournamentSize, rng);
                var parent2 = TournamentSelector.Select(population, fitnesslist, config.TournamentSize, rng);
                var child = PrecedenceOperationCrossover.Cross(parent1, parent2, jobIds, rng);
                child = SwapMutation.Mutate(child, config.MutationRate, rng);

                int childFitness = EvaluateFitness(child, jobLookup, cache);
                nextGen.Add(child);
                nextFit.Add(childFitness);
            }

            population = nextGen;
            fitnesslist = nextFit;

            int genBest = fitnesslist.Min();
            if (genBest < bestMakespan)
            {
                bestMakespan = genBest;
                bestIdx = fitnesslist.IndexOf(bestMakespan);
                bestChromosome = population[bestIdx];
                convergenceGen = gen;
            }

            OnConvergence?.Invoke(this, new ConvergenceEventArgs(gen, bestMakespan, config.Generations));
        }

        return (DecodeToSchedule(bestChromosome, jobLookup), bestMakespan, convergenceGen);
    }

    // -------------------------------------------------------------------------
    // Fitness evaluation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the makespan for <paramref name="chromosome"/>, reading from the
    /// cache if available and writing the result back after computation.
    /// </summary>
    private static int EvaluateFitness(
        Chromosome chromosome,
        Dictionary<int, Job> jobLookup,
        FitnessCache cache)
    {
        if (cache.TryGet(chromosome.HashKey, out int cached))
            return cached;

        int makespan = ComputeMakespan(chromosome.ToArray(), jobLookup);
        cache.Store(chromosome.HashKey, makespan);
        return makespan;
    }

    /// <summary>
    /// Evaluates the makespan of a chromosome given as a raw gene array.
    /// Exposed as <c>internal</c> so unit tests can verify the decoder directly
    /// via <c>[assembly: InternalsVisibleTo("JSSP.Tests")]</c>.
    /// </summary>
    /// <param name="genes">Job-index gene array to decode and evaluate.</param>
    /// <param name="jobs">Job list used to resolve each gene to an operation.</param>
    /// <returns>Makespan of the active schedule decoded from <paramref name="genes"/>.</returns>
    internal int FitnessFunction(int[] genes, List<Job> jobs) =>
        ComputeMakespan(genes, jobs.ToDictionary(j => j.Id));

    /// <summary>
    /// Active schedule decoding: scans <paramref name="genes"/> left-to-right,
    /// scheduling each operation at the earliest feasible time given machine
    /// and job precedence constraints. Returns the resulting makespan.
    /// </summary>
    /// <remarks>
    /// Time:  O(N) where N = total operations.
    /// Space: O(J + M) where J = distinct job count, M = distinct machine count.
    /// </remarks>
    private static int ComputeMakespan(int[] genes, Dictionary<int, Job> jobLookup)
    {
        var operationPointer = new Dictionary<int, int>(jobLookup.Count);
        var machineTime = new Dictionary<string, int>(jobLookup.Count * 2);
        var jobTime = new Dictionary<int, int>(jobLookup.Count);

        foreach (int id in jobLookup.Keys)
        {
            operationPointer[id] = 0;
            jobTime[id] = 0;
        }

        int makespan = 0;

        foreach (int jobId in genes)
        {
            var job = jobLookup[jobId];
            int opIdx = operationPointer[jobId]++;
            var op = job.Operations[opIdx];

            if (!machineTime.ContainsKey(op.Subdivision))
                machineTime[op.Subdivision] = 0;

            int startTime = Math.Max(machineTime[op.Subdivision], jobTime[jobId]);
            int completionTime = startTime + op.ProcessingTime;

            machineTime[op.Subdivision] = completionTime;
            jobTime[jobId] = completionTime;

            if (completionTime > makespan)
                makespan = completionTime;
        }

        return makespan;
    }

    // -------------------------------------------------------------------------
    // Schedule decoding
    // -------------------------------------------------------------------------

    /// <summary>
    /// Decodes a chromosome into a full <see cref="Schedule"/> recording each
    /// operation's start time, using the same active decoding logic as
    /// <see cref="ComputeMakespan"/>.
    /// </summary>
    private static Schedule DecodeToSchedule(Chromosome chromosome, Dictionary<int, Job> jobLookup)
    {
        var schedule = new Schedule();
        var operationPointer = new Dictionary<int, int>(jobLookup.Count);
        var machineTime = new Dictionary<string, int>(jobLookup.Count * 2);
        var jobTime = new Dictionary<int, int>(jobLookup.Count);

        foreach (int id in jobLookup.Keys)
        {
            operationPointer[id] = 0;
            jobTime[id] = 0;
        }

        for (int i = 0; i < chromosome.Length; i++)
        {
            int jobId = chromosome[i];
            var job = jobLookup[jobId];
            int opIdx = operationPointer[jobId]++;
            var op = job.Operations[opIdx];

            if (!machineTime.ContainsKey(op.Subdivision))
                machineTime[op.Subdivision] = 0;

            int startTime = Math.Max(machineTime[op.Subdivision], jobTime[jobId]);
            int completionTime = startTime + op.ProcessingTime;

            schedule.SetStartTime(op.JobId, op.OperationId, startTime, op.ProcessingTime);
            machineTime[op.Subdivision] = completionTime;
            jobTime[jobId] = completionTime;
        }

        return schedule;
    }
}
