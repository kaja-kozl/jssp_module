namespace JSSP.Algorithms.Genetic;

/// <summary>
/// K-way tournament selection for the genetic algorithm.
/// Draws <c>k</c> candidates at random and returns the one with the lowest
/// makespan (fitness). Lower makespan = higher selection pressure.
/// </summary>
internal static class TournamentSelector
{
    /// <summary>
    /// Selects one chromosome from <paramref name="population"/> via tournament selection.
    /// </summary>
    /// <param name="population">Current population of chromosomes.</param>
    /// <param name="fitnesses">
    /// Parallel list of makespan values. <c>fitnesses[i]</c> is the makespan of
    /// <c>population[i]</c>. Lower values are better.
    /// </param>
    /// <param name="tournamentSize">
    /// Number of candidates drawn per tournament. Larger values increase
    /// selection pressure; a value of 1 degrades to uniform random selection.
    /// </param>
    /// <param name="rng">Random number generator.</param>
    /// <returns>The chromosome with the lowest fitness among the tournament candidates.</returns>
    public static Chromosome Select(
        IReadOnlyList<Chromosome> population,
        IReadOnlyList<int> fitnesses,
        int tournamentSize,
        Random rng)
    {
        int bestIdx = rng.Next(population.Count);

        for (int i = 1; i < tournamentSize; i++)
        {
            int candidateIdx = rng.Next(population.Count);
            if (fitnesses[candidateIdx] < fitnesses[bestIdx])
                bestIdx = candidateIdx;
        }

        return population[bestIdx];
    }
}
