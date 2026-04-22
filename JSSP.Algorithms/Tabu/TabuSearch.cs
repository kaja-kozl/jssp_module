using JSSP.Algorithms.Constructive;
using JSSP.Core.Graph;
using JSSP.Core.Interfaces;
using JSSP.Core.Models;

namespace JSSP.Algorithms.Tabu;

/// <summary>
/// Tabu search for JSSP makespan minimisation using the N7 neighbourhood over the
/// critical path of the oriented disjunctive graph.
/// <para>
/// Each iteration: (1) find the critical path; (2) generate N7 candidate moves
/// (adjacent same-machine operation pairs on the critical path); (3) evaluate each
/// move via a temporary arc reversal; (4) accept the best non-tabu move, or any
/// tabu move that satisfies the aspiration criterion; (5) update the tabu list and
/// global best.
/// </para>
/// <para>
/// The critical path is recomputed from scratch after every move — it must not be
/// cached across <see cref="DisjunctiveGraph.ReverseArc"/> calls (per CLAUDE.md A2).
/// </para>
/// </summary>
public class TabuSearch : IAlgorithm
{
    /// <summary>
    /// Minimum total operation count across all jobs required to trigger the
    /// parallel-repetition path. Below this threshold the single-threaded path
    /// is used (overhead of thread creation exceeds benefit).
    /// </summary>
    private const int ParallelRepetitionThreshold = 10;

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

        var rng = new Random();
        var jobLookup = jobs.ToDictionary(j => j.Id);

        Schedule bestSchedule = null!;
        int overallBestMakespan = int.MaxValue;
        int bestConvergenceIter = 0;
        var repMakespans = new List<int>(config.Repetitions);

        for (int rep = 0; rep < config.Repetitions; rep++)
        {
            if (config.CancellationToken.IsCancellationRequested)
                break;

            var (schedule, makespan, convergenceIter) =
                RunOnce(jobs, jobLookup, config, rng);

            repMakespans.Add(makespan);

            if (makespan < overallBestMakespan)
            {
                overallBestMakespan = makespan;
                bestSchedule = schedule;
                bestConvergenceIter = convergenceIter;
            }
        }

        // Guard: if cancelled before any rep completed, build a greedy fallback.
        if (repMakespans.Count == 0)
        {
            int[] fallbackGenes = LargestRemainingWorkload.Create(jobs, rng);
            bestSchedule = DecodeToSchedule(fallbackGenes, jobLookup);
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
            ConvergenceGeneration = bestConvergenceIter,
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
    /// Executes a single full Tabu search run starting from a fresh LRW-constructed
    /// initial schedule. Delegates to <see cref="RunFromSchedule"/> after construction.
    /// </summary>
    private (Schedule schedule, int makespan, int convergenceIter) RunOnce(
        List<Job> jobs,
        Dictionary<int, Job> jobLookup,
        AlgorithmConfig config,
        Random rng)
    {
        int[] initialGenes = LargestRemainingWorkload.Create(jobs, rng);
        Schedule initialSchedule = DecodeToSchedule(initialGenes, jobLookup);
        return RunFromSchedule(initialSchedule, jobs, jobLookup, config, rng);
    }

    /// <summary>
    /// Executes a single Tabu search run starting from a caller-supplied schedule.
    /// Used by <see cref="JSSP.Algorithms.Memetic.MemeticHybrid"/> to apply local
    /// search to the best chromosome decoded from the GA population each injection cycle.
    /// </summary>
    /// <param name="initialSchedule">
    /// Starting point for the search. The disjunctive graph is oriented from this
    /// schedule on entry.
    /// </param>
    /// <param name="jobs">All jobs in the instance.</param>
    /// <param name="jobLookup">Dictionary mapping job ID to <see cref="Job"/>.</param>
    /// <param name="config">
    /// Algorithm configuration. <c>config.Generations</c> controls the number of
    /// Tabu iterations; <c>config.TabuListLength</c> controls the tabu tenure.
    /// </param>
    /// <param name="rng">Random source (used for stagnation tie-breaking if added later).</param>
    /// <returns>
    /// Tuple of (best schedule found, its makespan, iteration at which it was first found).
    /// </returns>
    internal (Schedule schedule, int makespan, int convergenceIter) RunFromSchedule(
        Schedule initialSchedule,
        List<Job> jobs,
        Dictionary<int, Job> jobLookup,
        AlgorithmConfig config,
        Random rng)
    {
        // Orient the disjunctive graph from the provided starting schedule.
        var graph = DisjunctiveGraph.Build(jobs);
        graph.Orient(initialSchedule);

        var cpFinder = new CriticalPathFinder();
        var tabuList = new TabuList(config.TabuListLength);

        int repBestMakespan = initialSchedule.Makespan;
        Schedule repBestSchedule = initialSchedule;
        int convergenceIter = 0;

        // Tracks the global best seen so far (for aspiration criterion).
        int globalBestSoFar = repBestMakespan;

        for (int iter = 1; iter <= config.Generations; iter++)
        {
            if (config.CancellationToken.IsCancellationRequested)
                break;

            // 1. Find the critical path and generate candidate moves.
            var criticalPath = cpFinder.Find(graph);
            var moves = N7Neighbourhood.Generate(criticalPath, graph);

            if (moves.Count == 0)
            {
                OnConvergence?.Invoke(this, new ConvergenceEventArgs(iter, repBestMakespan, config.Generations));
                break;
            }

            // 2. Evaluate all candidate moves; track the best permissible one.
            GraphNode? bestFrom = null;
            GraphNode? bestTo = null;
            int bestCandidateMakespan = int.MaxValue;

            foreach (var (from, to) in moves)
            {
                var move = new TabuMove(from.NodeId, to.NodeId);
                bool isTabu = tabuList.IsTabu(move);

                graph.ReverseArc(from, to);
                bool acyclic = cpFinder.TryFindMakespan(graph, out int candidateMakespan);
                graph.ReverseArc(to, from);

                if (!acyclic)
                    continue;

                if (isTabu && !AspirationCriterion.IsSatisfied(candidateMakespan, globalBestSoFar))
                    continue;

                if (candidateMakespan < bestCandidateMakespan)
                {
                    bestCandidateMakespan = candidateMakespan;
                    bestFrom = from;
                    bestTo = to;
                }
            }

            // 3. Force-accept the best non-cycle move when all are tabu (anti-stagnation).
            if (bestFrom is null)
            {
                foreach (var (from, to) in moves)
                {
                    graph.ReverseArc(from, to);
                    bool acyclic = cpFinder.TryFindMakespan(graph, out int candidateMakespan);
                    graph.ReverseArc(to, from);

                    if (!acyclic)
                        continue;

                    if (candidateMakespan < bestCandidateMakespan)
                    {
                        bestCandidateMakespan = candidateMakespan;
                        bestFrom = from;
                        bestTo = to;
                    }
                }
            }

            // 4. Apply the accepted move.
            if (bestFrom is not null && bestTo is not null)
            {
                graph.ReverseArc(bestFrom, bestTo);
                tabuList.Add(new TabuMove(bestTo.NodeId, bestFrom.NodeId));

                var currentSchedule = cpFinder.FindSchedule(graph);

                if (bestCandidateMakespan < repBestMakespan)
                {
                    repBestMakespan = bestCandidateMakespan;
                    repBestSchedule = currentSchedule;
                    convergenceIter = iter;
                }

                if (bestCandidateMakespan < globalBestSoFar)
                    globalBestSoFar = bestCandidateMakespan;
            }

            OnConvergence?.Invoke(this, new ConvergenceEventArgs(iter, repBestMakespan, config.Generations));
        }

        return (repBestSchedule, repBestMakespan, convergenceIter);
    }

    // -------------------------------------------------------------------------
    // Schedule decoding (active scheduling from a gene array)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Decodes a job-based gene array produced by a constructive heuristic into
    /// a full <see cref="Schedule"/> using the active scheduling rule:
    /// each operation starts at the earliest time permitted by both machine and
    /// job-precedence constraints.
    /// </summary>
    /// <param name="genes">
    /// Job-index gene array. Gene value <c>j</c> represents job <c>j</c>;
    /// job <c>j</c> appears exactly as many times as it has operations.
    /// </param>
    /// <param name="jobLookup">
    /// Dictionary mapping job ID to <see cref="Job"/>. Job IDs may be non-contiguous.
    /// </param>
    /// <returns>A feasible <see cref="Schedule"/> for the given gene sequence.</returns>
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
}
