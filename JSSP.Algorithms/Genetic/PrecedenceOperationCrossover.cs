namespace JSSP.Algorithms.Genetic;

/// <summary>
/// Precedence Operation Crossover (POX) for JSSP job-based chromosomes,
/// as used in the MAGATS paper (PLOS ONE, 2019).
///
/// Algorithm:
///   1. Randomly partition the job set into two non-empty subsets J1 and J2.
///   2. Copy all genes belonging to J1 from parent1 into the child (preserving
///      their relative left-to-right order from parent1).
///   3. Fill the remaining child positions with J2 genes taken from parent2
///      in the order they appear in parent2.
///
/// Feasibility guarantee: each job index appears the same number of times
/// in the child as in both parents — no repair operator is required.
/// </summary>
internal static class PrecedenceOperationCrossover
{
    /// <summary>
    /// Produces one offspring chromosome from two parents via POX.
    /// </summary>
    /// <param name="parent1">First parent; J1 operations are inherited from this parent.</param>
    /// <param name="parent2">Second parent; J2 operations are inherited from this parent.</param>
    /// <param name="jobIds">Distinct job IDs present in the chromosome.</param>
    /// <param name="rng">Random number generator for the job-set partition.</param>
    /// <returns>A new feasible child chromosome.</returns>
    public static Chromosome Cross(
        Chromosome parent1,
        Chromosome parent2,
        IReadOnlyList<int> jobIds,
        Random rng)
    {
        // Single-job problems cannot meaningfully combine two parents.
        if (jobIds.Count < 2)
            return parent1;

        var j1 = SelectJ1(jobIds, rng);

        var child = new int[parent1.Length];
        int childIdx = 0;

        // Phase 1: copy J1 genes from parent1 in order.
        for (int i = 0; i < parent1.Length; i++)
        {
            if (j1.Contains(parent1[i]))
                child[childIdx++] = parent1[i];
        }

        // Phase 2: fill remaining positions with J2 genes from parent2 in order.
        for (int i = 0; i < parent2.Length; i++)
        {
            if (!j1.Contains(parent2[i]))
                child[childIdx++] = parent2[i];
        }

        return Chromosome.FromArray(child);
    }

    /// <summary>
    /// Partitions <paramref name="jobIds"/> into subset J1 at random, guaranteeing
    /// that both J1 and the implied J2 are non-empty: the first job is always
    /// assigned to J1 and the last job is always assigned to J2; all others
    /// are assigned with probability 0.5.
    /// </summary>
    private static HashSet<int> SelectJ1(IReadOnlyList<int> jobIds, Random rng)
    {
        var j1 = new HashSet<int> { jobIds[0] };

        // The last job is reserved for J2 to guarantee non-empty J2.
        // Middle jobs are assigned randomly.
        for (int i = 1; i < jobIds.Count - 1; i++)
        {
            if (rng.NextDouble() < 0.5)
                j1.Add(jobIds[i]);
        }

        // jobIds[^1] is intentionally left out of j1 (belongs to J2).
        return j1;
    }
}
