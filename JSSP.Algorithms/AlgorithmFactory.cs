using JSSP.Algorithms.Genetic;
using JSSP.Algorithms.Memetic;
using JSSP.Algorithms.Tabu;
using JSSP.Core.Interfaces;

namespace JSSP.Algorithms;

/// <summary>
/// Factory that instantiates the correct <see cref="IAlgorithm"/> implementation by name.
/// Keeps the orchestration layer decoupled from concrete algorithm types (OCP).
/// </summary>
public static class AlgorithmFactory
{
    private static readonly IReadOnlyList<string> _names =
    [
        nameof(GeneticAlgorithm),
        nameof(TabuSearch),
        nameof(MemeticHybrid),
        nameof(BranchBound)
    ];

    /// <summary>Returns the display names of all available algorithm implementations.</summary>
    public static IReadOnlyList<string> GetAvailableAlgorithms() => _names;

    /// <summary>
    /// Creates and returns the <see cref="IAlgorithm"/> identified by <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The class name of the algorithm to instantiate.</param>
    /// <returns>A new instance of the requested algorithm.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> does not match any known algorithm.
    /// </exception>
    public static IAlgorithm Create(string name) => name switch
    {
        nameof(GeneticAlgorithm) => new GeneticAlgorithm(),
        nameof(TabuSearch) => new TabuSearch(),
        nameof(MemeticHybrid) => new MemeticHybrid(),
        nameof(BranchBound) => new BranchBound(),
        _ => throw new ArgumentException($"Unknown algorithm: {name}", nameof(name))
    };
}
