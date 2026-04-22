using JSSP.Core.Models;

namespace JSSP.Algorithms.Constructive;

/// <summary>
/// Constructive heuristic that builds a job-based gene sequence by greedily
/// scheduling the operation belonging to the job with the largest remaining
/// workload (total unscheduled processing time). Ties are broken by random
/// selection among the tied candidates.
/// </summary>
/// <remarks>
/// <para>
/// The returned <c>int[]</c> uses the same job-based encoding as the GA:
/// each element is a job ID, and job ID <c>j</c> appears exactly as many times
/// as job <c>j</c> has operations. Any permutation satisfying this property is
/// feasible — no repair is required.
/// </para>
/// <para>
/// Time:  O(N × J) — N total operations, J distinct jobs.
/// Space: O(J + N) — workload/pointer dictionaries plus the output array.
/// </para>
/// </remarks>
public static class LargestRemainingWorkload
{
    /// <summary>
    /// Creates a job-based gene array using the Largest Remaining Workload heuristic.
    /// At each step the next operation whose job has the highest remaining processing
    /// time is appended. When two or more jobs are tied, one is chosen at random.
    /// </summary>
    /// <param name="jobs">Job list defining the JSSP instance.</param>
    /// <param name="rng">Random number generator used for tie-breaking.</param>
    /// <returns>
    /// A feasible gene array of length equal to the total number of operations
    /// across all jobs, suitable for wrapping with <c>Chromosome.FromArray</c>.
    /// </returns>
    public static int[] Create(List<Job> jobs, Random rng)
    {
        int totalOps = jobs.Sum(j => j.Operations.Count);
        var genes = new int[totalOps];

        // remainingWorkload[jobId] = sum of ProcessingTime for all not-yet-placed ops.
        var remainingWorkload = new Dictionary<int, int>(jobs.Count);

        // operationPointer[jobId] = index of the next operation to place for that job.
        var operationPointer = new Dictionary<int, int>(jobs.Count);

        // jobLookup allows O(1) access to a Job by its ID.
        var jobLookup = new Dictionary<int, Job>(jobs.Count);

        foreach (var job in jobs)
        {
            remainingWorkload[job.Id] = job.Operations.Sum(op => op.ProcessingTime);
            operationPointer[job.Id] = 0;
            jobLookup[job.Id] = job;
        }

        // Reusable buffers — avoid re-allocating per iteration.
        var candidates = new List<int>(jobs.Count);
        var ties = new List<int>(jobs.Count);

        for (int geneIdx = 0; geneIdx < totalOps; geneIdx++)
        {
            // Collect all jobs that still have at least one unplaced operation.
            candidates.Clear();
            foreach (var job in jobs)
            {
                if (operationPointer[job.Id] < job.Operations.Count)
                    candidates.Add(job.Id);
            }

            // Find the maximum remaining workload among candidates.
            int maxWorkload = int.MinValue;
            foreach (int jobId in candidates)
            {
                if (remainingWorkload[jobId] > maxWorkload)
                    maxWorkload = remainingWorkload[jobId];
            }

            // Collect all candidates that share the maximum workload.
            ties.Clear();
            foreach (int jobId in candidates)
            {
                if (remainingWorkload[jobId] == maxWorkload)
                    ties.Add(jobId);
            }

            // Break ties randomly for population diversity.
            int chosen = ties[rng.Next(ties.Count)];

            // Record the gene and advance the chosen job's operation pointer.
            int opIdx = operationPointer[chosen];
            genes[geneIdx] = chosen;
            remainingWorkload[chosen] -= jobLookup[chosen].Operations[opIdx].ProcessingTime;
            operationPointer[chosen]++;
        }

        return genes;
    }
}
