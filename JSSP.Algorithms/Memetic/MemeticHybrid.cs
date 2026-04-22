using JSSP.Algorithms.Constructive;
using JSSP.Algorithms.Genetic;
using JSSP.Algorithms.Tabu;
using JSSP.Core.Interfaces;
using JSSP.Core.Models;

namespace JSSP.Algorithms.Memetic;

/// <summary>
/// Memetic algorithm that interleaves a genetic algorithm's global exploration with
/// Tabu search local exploitation. Every <see cref="AlgorithmConfig.InjectionFrequencyK"/>
/// generations the best chromosome in the population is decoded to a schedule, improved
/// by a short Tabu run, re-encoded, and injected back into the population in place of
/// the worst individuals.
///
/// <para>
/// Uses composition — does not inherit from <see cref="Genetic.GeneticAlgorithm"/> or
/// <see cref="Tabu.TabuSearch"/> to avoid the fragile base class problem (CLAUDE.md A3).
/// </para>
/// </summary>
public class MemeticHybrid : IAlgorithm
{
    private AlgorithmStats? _stats;

    /// <inheritdoc/>
    public event EventHandler<ConvergenceEventArgs>? OnConvergence;

    // -------------------------------------------------------------------------
    // IAlgorithm.Solve
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public Schedule Solve(List<Job> jobs, AlgorithmConfig config)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var jobIds = jobs.Select(j => j.Id).ToList();
        var jobLookup = jobs.ToDictionary(j => j.Id);

        var tabu = new TabuSearch();

        Schedule bestSchedule = null!;
        int overallBestMakespan = int.MaxValue;
        int bestConvergenceGen = 0;
        var repMakespans = new List<int>(config.Repetitions);

        for (int rep = 0; rep < config.Repetitions; rep++)
        {
            if (config.CancellationToken.IsCancellationRequested)
                break;

            var rng = new Random();
            var (schedule, makespan, convergenceGen) =
                RunOnce(jobs, jobIds, jobLookup, config, tabu, rng);

            repMakespans.Add(makespan);

            if (makespan < overallBestMakespan)
            {
                overallBestMakespan = makespan;
                bestSchedule = schedule;
                bestConvergenceGen = convergenceGen;
            }
        }

        // Guard: cancelled before any rep completed — decode one random chromosome.
        if (repMakespans.Count == 0)
        {
            var fallbackRng = new Random();
            int[] fallback = LargestRemainingWorkload.Create(jobs, fallbackRng);
            bestSchedule = DecodeToSchedule(fallback, jobLookup);
            overallBestMakespan = bestSchedule.Makespan;
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
    // Core loop — one independent repetition
    // -------------------------------------------------------------------------

    /// <summary>
    /// Executes one full memetic run. Maintains a GA population and injects Tabu
    /// local search every <c>config.InjectionFrequencyK</c> generations.
    /// </summary>
    private (Schedule schedule, int makespan, int convergenceGen) RunOnce(
        List<Job> jobs,
        IReadOnlyList<int> jobIds,
        Dictionary<int, Job> jobLookup,
        AlgorithmConfig config,
        TabuSearch tabu,
        Random rng)
    {
        // Tabu sub-run config: iterations scale with problem size, not tabu tenure.
        // totalOps * 5 gives Tabu enough moves to meaningfully improve the solution;
        // the floor of 50 prevents degenerate behaviour on tiny instances.
        int totalOps = jobs.Sum(j => j.Operations.Count);
        var tabuConfig = new AlgorithmConfig
        {
            Repetitions = 1,
            Generations = Math.Clamp(totalOps, 30, 100),
            TabuListLength = config.TabuListLength,
            CancellationToken = config.CancellationToken,
        };

        // --- Initialise population (LRW seed + random diversity, identical to GA) ---
        var population = new List<Chromosome>(config.PopulationSize);
        population.Add(Chromosome.FromArray(LargestRemainingWorkload.Create(jobs, rng)));
        for (int i = 1; i < config.PopulationSize; i++)
            population.Add(Chromosome.CreateRandom(jobs, rng));

        var cache = new FitnessCache();

        // Evaluate initial population.
        var fitnesses = population
            .Select(c => EvaluateFitness(c, jobLookup, cache))
            .ToList();

        int bestMakespan = fitnesses.Min();
        int bestIdx = fitnesses.IndexOf(bestMakespan);
        Chromosome bestChromosome = population[bestIdx];
        int convergenceGen = 0;

        // --- Generational loop ---
        for (int gen = 1; gen <= config.Generations; gen++)
        {
            if (config.CancellationToken.IsCancellationRequested)
                break;

            var nextGen = new List<Chromosome>(config.PopulationSize);
            var nextFit = new List<int>(config.PopulationSize);

            // Elitism: carry the best EliteCount individuals unchanged.
            var eliteIndices = fitnesses
                .Select((f, i) => (f, i))
                .OrderBy(t => t.f)
                .Take(config.EliteCount)
                .Select(t => t.i)
                .ToList();

            foreach (int ei in eliteIndices)
            {
                nextGen.Add(population[ei]);
                nextFit.Add(fitnesses[ei]);
            }

            // GA step: tournament selection → POX crossover → swap mutation.
            while (nextGen.Count < config.PopulationSize)
            {
                var parent1 = TournamentSelector.Select(population, fitnesses, config.TournamentSize, rng);
                var parent2 = TournamentSelector.Select(population, fitnesses, config.TournamentSize, rng);
                var child = PrecedenceOperationCrossover.Cross(parent1, parent2, jobIds, rng);
                child = SwapMutation.Mutate(child, config.MutationRate, rng);

                int childFitness = EvaluateFitness(child, jobLookup, cache);
                nextGen.Add(child);
                nextFit.Add(childFitness);
            }

            population = nextGen;
            fitnesses = nextFit;

            int genBest = fitnesses.Min();
            if (genBest < bestMakespan)
            {
                bestMakespan = genBest;
                bestIdx = fitnesses.IndexOf(bestMakespan);
                bestChromosome = population[bestIdx];
                convergenceGen = gen;
            }

            // Tabu injection every K generations.
            if (gen % config.InjectionFrequencyK == 0)
            {
                Schedule seedSchedule = DecodeToSchedule(bestChromosome.ToArray(), jobLookup);
                var (improvedSchedule, improvedMakespan, _) =
                    tabu.RunFromSchedule(seedSchedule, jobs, jobLookup, tabuConfig, rng);

                // Re-encode before comparing: ScheduleToChromosome → DecodeToSchedule
                // is not a perfect round-trip, so evaluate the re-encoded chromosome
                // directly rather than trusting improvedMakespan from the Tabu run.
                Chromosome injected = ScheduleToChromosome(improvedSchedule, jobs);
                int injectedFitness = EvaluateFitness(injected, jobLookup, cache);

                if (injectedFitness < bestMakespan)
                {
                    // Replace the individual that was seeded (bestIdx) with the
                    // improved version. This preserves population diversity — replacing
                    // the worst individuals instead would silently duplicate chromosomes.
                    population[bestIdx] = injected;
                    fitnesses[bestIdx] = injectedFitness;

                    bestMakespan = injectedFitness;
                    bestChromosome = injected;
                    convergenceGen = gen;
                }
            }

            OnConvergence?.Invoke(this, new ConvergenceEventArgs(gen, bestMakespan, config.Generations));
        }

        return (DecodeToSchedule(bestChromosome.ToArray(), jobLookup), bestMakespan, convergenceGen);
    }

    // -------------------------------------------------------------------------
    // Fitness evaluation
    // -------------------------------------------------------------------------

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
    // Schedule decoding and re-encoding
    // -------------------------------------------------------------------------

    /// <summary>
    /// Active schedule decoding: each operation starts at the earliest time
    /// permitted by its machine and job predecessor.
    /// </summary>
    private static Schedule DecodeToSchedule(int[] genes, Dictionary<int, Job> jobLookup)
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

        foreach (int jobId in genes)
        {
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

    /// <summary>
    /// Reconstructs a job-based chromosome from a schedule by sorting all operations
    /// by start time. Ties are broken by job ID then operation ID, preserving the
    /// strict per-job ordering that a feasible schedule guarantees.
    /// </summary>
    private static Chromosome ScheduleToChromosome(Schedule schedule, List<Job> jobs)
    {
        var entries = new List<(int Start, int JobId, int OperationId)>(
            jobs.Sum(j => j.Operations.Count));

        foreach (var job in jobs)
            foreach (var op in job.Operations)
                entries.Add((schedule.GetStartTime(op.JobId, op.OperationId), op.JobId, op.OperationId));

        // Stable sort: start time first, then job/op for determinism when times equal.
        entries.Sort((a, b) =>
            a.Start != b.Start ? a.Start.CompareTo(b.Start)
                : a.JobId != b.JobId ? a.JobId.CompareTo(b.JobId)
                    : a.OperationId.CompareTo(b.OperationId));

        var genes = new int[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            genes[i] = entries[i].JobId;

        return Chromosome.FromArray(genes);
    }
}
