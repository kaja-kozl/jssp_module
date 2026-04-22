using JSSP.Core.Interfaces;
using JSSP.Core.Models;

namespace JSSP.Algorithms;

/// <summary>
/// Exact Branch-and-Bound solver for small JSSP instances.
/// Guarantees the optimal makespan via exhaustive DFS with lower-bound pruning.
/// Practical only for small instances (≤ <see cref="MaxTractableOperations"/> total
/// operations) — worst-case complexity is O(n!).
/// </summary>
public class BranchBound : IAlgorithm
{
    /// <summary>
    /// Maximum total operations for which Branch &amp; Bound is considered tractable.
    /// Beyond this threshold the O(n!) search space makes completion time unpredictable.
    /// </summary>
    public const int MaxTractableOperations = 20;

    private AlgorithmStats? _stats;
    private int _nodesExplored;
    private int _improvementCount;

    /// <summary>
    /// Returns <see langword="true"/> when the total operation count across all jobs
    /// is within the tractable limit (<see cref="MaxTractableOperations"/>).
    /// </summary>
    /// <param name="jobs">The job list to evaluate.</param>
    public static bool IsViable(List<Job> jobs) =>
        jobs.Sum(j => j.Operations.Count) <= MaxTractableOperations;

    /// <inheritdoc/>
    public event EventHandler<ConvergenceEventArgs>? OnConvergence;

    /// <inheritdoc/>
    public Schedule Solve(List<Job> jobs, AlgorithmConfig config)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _nodesExplored = 0;
        _improvementCount = 0;

        int totalOps = jobs.Sum(j => j.Operations.Count);

        if (totalOps > MaxTractableOperations)
            throw new InvalidOperationException(
                $"Instance has {totalOps} total operations. " +
                $"Branch & Bound is only viable for ≤ {MaxTractableOperations} operations.");

        var machineTime = new Dictionary<string, int>();
        var jobTime = new Dictionary<int, int>();
        var opPointer = new Dictionary<int, int>();

        foreach (var job in jobs)
        {
            jobTime[job.Id] = 0;
            opPointer[job.Id] = 0;
            foreach (var op in job.Operations)
            {
                if (!machineTime.ContainsKey(op.Subdivision))
                    machineTime[op.Subdivision] = 0;
            }
        }

        var startTimes = new Dictionary<(int jobId, int operationId), int>();
        int bestMakespan = int.MaxValue;
        Dictionary<(int, int), int>? bestStartTimes = null;

        Search(
            jobs,
            opPointer,
            machineTime,
            jobTime,
            startTimes,
            ref bestMakespan,
            ref bestStartTimes,
            0,
            totalOps,
            config);

        var schedule = new Schedule();
        if (bestStartTimes != null)
        {
            foreach (var job in jobs)
            {
                foreach (var op in job.Operations)
                {
                    int start = bestStartTimes[(op.JobId, op.OperationId)];
                    schedule.SetStartTime(op.JobId, op.OperationId, start, op.ProcessingTime);
                }
            }
        }

        sw.Stop();

        _stats = new AlgorithmStats
        {
            BestMakespan = schedule.Makespan,
            MeanMakespan = schedule.Makespan,
            StdDev = 0.0,
            ConvergenceGeneration = _nodesExplored,
            ElapsedTime = sw.Elapsed,
        };

        return schedule;
    }

    private void Search(
        List<Job> jobs,
        Dictionary<int, int> opPointer,
        Dictionary<string, int> machineTime,
        Dictionary<int, int> jobTime,
        Dictionary<(int, int), int> startTimes,
        ref int bestMakespan,
        ref Dictionary<(int, int), int>? bestStartTimes,
        int depth,
        int totalOps,
        AlgorithmConfig config)
    {
        if (config.CancellationToken.IsCancellationRequested)
            return;

        _nodesExplored++;

        if (depth == totalOps)
        {
            int makespan = jobs.Max(j => jobTime[j.Id]);
            if (makespan < bestMakespan)
            {
                bestMakespan = makespan;
                bestStartTimes = new Dictionary<(int, int), int>(startTimes);
                _improvementCount++;
                // TotalIterations = -1 signals to the UI that this is an unbounded search;
                // Generation carries the improvement counter so the graph has meaningful x values.
                OnConvergence?.Invoke(this, new ConvergenceEventArgs(_improvementCount, bestMakespan, -1));
            }
            return;
        }

        int lb = ComputeLowerBound(jobs, opPointer, machineTime, jobTime);
        if (lb >= bestMakespan)
            return;

        foreach (var job in jobs)
        {
            int ptr = opPointer[job.Id];
            if (ptr >= job.Operations.Count)
                continue;

            var op = job.Operations[ptr];
            int start = Math.Max(machineTime[op.Subdivision], jobTime[op.JobId]);
            int finish = start + op.ProcessingTime;

            int oldMachineTime = machineTime[op.Subdivision];
            int oldJobTime = jobTime[op.JobId];

            machineTime[op.Subdivision] = finish;
            jobTime[op.JobId] = finish;
            opPointer[op.JobId] = ptr + 1;
            startTimes[(op.JobId, op.OperationId)] = start;

            Search(jobs, opPointer, machineTime, jobTime, startTimes,
                   ref bestMakespan, ref bestStartTimes, depth + 1, totalOps, config);

            machineTime[op.Subdivision] = oldMachineTime;
            jobTime[op.JobId] = oldJobTime;
            opPointer[op.JobId] = ptr;
            startTimes.Remove((op.JobId, op.OperationId));
        }
    }

    /// <summary>
    /// Computes a lower bound on the makespan of any schedule that extends the current
    /// partial assignment. Uses two independent bounds and returns the maximum:
    /// <list type="bullet">
    ///   <item>Machine LB: for each machine, its current free time plus all remaining
    ///   processing time assigned to it — the machine cannot finish sooner.</item>
    ///   <item>Job LB: for each job, its current completion time plus all remaining
    ///   processing time in the job chain — no job can finish sooner.</item>
    /// </list>
    /// </summary>
    private static int ComputeLowerBound(
        List<Job> jobs,
        Dictionary<int, int> opPointer,
        Dictionary<string, int> machineTime,
        Dictionary<int, int> jobTime)
    {
        var machineLbValues = new Dictionary<string, int>(machineTime);
        int jobLb = 0;

        foreach (var job in jobs)
        {
            int ptr = opPointer[job.Id];
            int remainingJobTime = 0;

            for (int i = ptr; i < job.Operations.Count; i++)
            {
                var op = job.Operations[i];
                remainingJobTime += op.ProcessingTime;
                machineLbValues[op.Subdivision] += op.ProcessingTime;
            }

            int jobFinish = jobTime[job.Id] + remainingJobTime;
            if (jobFinish > jobLb)
                jobLb = jobFinish;
        }

        int machineLb = machineLbValues.Count > 0 ? machineLbValues.Values.Max() : 0;
        return Math.Max(machineLb, jobLb);
    }

    /// <inheritdoc/>
    public AlgorithmStats? GetStats() => _stats;
}
