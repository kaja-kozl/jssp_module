using JSSP.Core.Models;

namespace JSSP.Algorithms.HyperParameter;

/// <summary>
/// Defines the hyperparameter search space for JSSP algorithms and enumerates
/// all grid points (Cartesian product of each axis) or samples them randomly.
/// <para>
/// Default axis ranges are taken from CLAUDE.md A7, which are in turn informed
/// by the MAGATS paper (PLOS ONE 2019).
/// </para>
/// </summary>
public static class ParameterGrid
{
    // Default axis values — each array is one dimension of the grid.

    /// <summary>Population size axis: 50 to 200 in steps of 25.</summary>
    public static readonly IReadOnlyList<int> DefaultPopulationSizes =
        Enumerable.Range(0, 7).Select(i => 50 + i * 25).ToArray();

    /// <summary>Mutation rate axis: 0.01 to 0.10 in steps of 0.01.</summary>
    public static readonly IReadOnlyList<double> DefaultMutationRates =
        Enumerable.Range(1, 10).Select(i => Math.Round(i * 0.01, 2)).ToArray();

    /// <summary>Tournament size axis: 3 to 7.</summary>
    public static readonly IReadOnlyList<int> DefaultTournamentSizes = [3, 4, 5, 6, 7];

    /// <summary>Tabu list length axis: 5 to 20 in steps of 5.</summary>
    public static readonly IReadOnlyList<int> DefaultTabuListLengths = [5, 10, 15, 20];

    /// <summary>Injection frequency K axis: 5 to 25 in steps of 5.</summary>
    public static readonly IReadOnlyList<int> DefaultInjectionFrequencies = [5, 10, 15, 20, 25];

    /// <summary>
    /// Enumerates every <see cref="AlgorithmConfig"/> in the Cartesian product of
    /// the provided axes. All other config fields (Generations, Repetitions, etc.)
    /// are kept at their <see cref="AlgorithmConfig"/> defaults.
    /// </summary>
    /// <param name="populationSizes">
    /// Population size values to sweep. Defaults to <see cref="DefaultPopulationSizes"/>.
    /// </param>
    /// <param name="mutationRates">
    /// Mutation rate values to sweep. Defaults to <see cref="DefaultMutationRates"/>.
    /// </param>
    /// <param name="tournamentSizes">
    /// Tournament size values to sweep. Defaults to <see cref="DefaultTournamentSizes"/>.
    /// </param>
    /// <param name="tabuListLengths">
    /// Tabu list length values to sweep. Defaults to <see cref="DefaultTabuListLengths"/>.
    /// </param>
    /// <param name="injectionFrequencies">
    /// Injection frequency K values to sweep. Defaults to <see cref="DefaultInjectionFrequencies"/>.
    /// </param>
    /// <param name="generations">
    /// Generations per trial run. Defaults to 100 to keep total grid search time manageable.
    /// </param>
    /// <param name="repetitions">
    /// Repetitions per trial run. Defaults to 3 for statistical stability.
    /// </param>
    /// <returns>All grid-point configurations in population × mutation × tournament × tabu × injection order.</returns>
    public static IEnumerable<AlgorithmConfig> Enumerate(
        IReadOnlyList<int>? populationSizes = null,
        IReadOnlyList<double>? mutationRates = null,
        IReadOnlyList<int>? tournamentSizes = null,
        IReadOnlyList<int>? tabuListLengths = null,
        IReadOnlyList<int>? injectionFrequencies = null,
        int generations = 100,
        int repetitions = 3)
    {
        var pops = populationSizes ?? DefaultPopulationSizes;
        var muts = mutationRates ?? DefaultMutationRates;
        var tours = tournamentSizes ?? DefaultTournamentSizes;
        var tabus = tabuListLengths ?? DefaultTabuListLengths;
        var injs = injectionFrequencies ?? DefaultInjectionFrequencies;

        foreach (int pop in pops)
            foreach (double mut in muts)
                foreach (int tour in tours)
                    foreach (int tabu in tabus)
                        foreach (int inj in injs)
                            yield return new AlgorithmConfig
                            {
                                PopulationSize = pop,
                                MutationRate = mut,
                                TournamentSize = tour,
                                TabuListLength = tabu,
                                InjectionFrequencyK = inj,
                                Generations = generations,
                                Repetitions = repetitions,
                            };
    }

    /// <summary>
    /// Samples <paramref name="count"/> random configurations from the full
    /// parameter grid without replacement. If <paramref name="count"/> exceeds
    /// the total grid size, all configurations are returned.
    /// </summary>
    /// <param name="count">Number of configurations to sample.</param>
    /// <param name="rng">Random source for sampling.</param>
    /// <param name="populationSizes">Override for the population size axis.</param>
    /// <param name="mutationRates">Override for the mutation rate axis.</param>
    /// <param name="tournamentSizes">Override for the tournament size axis.</param>
    /// <param name="tabuListLengths">Override for the tabu list length axis.</param>
    /// <param name="injectionFrequencies">Override for the injection frequency axis.</param>
    /// <param name="generations">Generations per trial run.</param>
    /// <param name="repetitions">Repetitions per trial run.</param>
    /// <returns>
    /// Up to <paramref name="count"/> randomly selected, distinct grid-point configurations.
    /// </returns>
    public static IReadOnlyList<AlgorithmConfig> Sample(
        int count,
        Random rng,
        IReadOnlyList<int>? populationSizes = null,
        IReadOnlyList<double>? mutationRates = null,
        IReadOnlyList<int>? tournamentSizes = null,
        IReadOnlyList<int>? tabuListLengths = null,
        IReadOnlyList<int>? injectionFrequencies = null,
        int generations = 100,
        int repetitions = 3)
    {
        var all = Enumerate(
            populationSizes, mutationRates, tournamentSizes,
            tabuListLengths, injectionFrequencies,
            generations, repetitions).ToList();

        // Fisher-Yates partial shuffle — equivalent to sampling without replacement.
        int take = Math.Min(count, all.Count);
        for (int i = 0; i < take; i++)
        {
            int j = rng.Next(i, all.Count);
            (all[i], all[j]) = (all[j], all[i]);
        }

        return all.Take(take).ToList().AsReadOnly();
    }
}
