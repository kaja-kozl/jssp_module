namespace JSSP.Algorithms.Genetic;

/// <summary>
/// Swap mutation for job-based JSSP chromosomes.
/// Selects two positions at random and exchanges their genes.
/// Feasibility is preserved because swapping job indices maintains
/// the per-job operation count invariant.
/// Applied stochastically: returns the original chromosome unchanged
/// when the drawn probability exceeds <c>mutationRate</c>.
/// </summary>
internal static class SwapMutation
{
    /// <summary>
    /// Applies swap mutation to <paramref name="chromosome"/> with the given probability.
    /// </summary>
    /// <param name="chromosome">Chromosome to (potentially) mutate.</param>
    /// <param name="mutationRate">
    /// Probability in [0, 1] that mutation is applied.
    /// Corresponds to <see cref="JSSP.Core.Models.AlgorithmConfig.MutationRate"/>.
    /// </param>
    /// <param name="rng">Random number generator.</param>
    /// <returns>
    /// A mutated copy if the probability test passes; the original
    /// <paramref name="chromosome"/> reference otherwise (no allocation).
    /// </returns>
    public static Chromosome Mutate(Chromosome chromosome, double mutationRate, Random rng)
    {
        if (rng.NextDouble() >= mutationRate)
            return chromosome;

        var genes = chromosome.ToArray();
        int i = rng.Next(genes.Length);
        int j = rng.Next(genes.Length);
        (genes[i], genes[j]) = (genes[j], genes[i]);
        return Chromosome.FromArray(genes);
    }
}
