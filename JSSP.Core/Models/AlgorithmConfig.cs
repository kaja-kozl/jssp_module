namespace JSSP.Core.Models;

/// <summary>
/// Hyperparameter bag passed to every algorithm implementation.
/// Default values are informed by the PLOS ONE multi-agent GA+Tabu paper.
/// </summary>
public class AlgorithmConfig
{
    /// <summary>Gets or inits the number of individuals in the GA population.</summary>
    public int PopulationSize { get; init; } = 150;

    /// <summary>Gets or inits the probability of mutating a gene.</summary>
    public double MutationRate { get; init; } = 0.02;

    /// <summary>Gets or inits the tournament size for parent selection.</summary>
    public int TournamentSize { get; init; } = 5;

    /// <summary>Gets or inits the maximum Tabu list length.</summary>
    public int TabuListLength { get; init; } = 10;

    /// <summary>Gets or inits the Tabu injection frequency K used by the memetic algorithm.</summary>
    public int InjectionFrequencyK { get; init; } = 10;

    /// <summary>Gets or inits the number of generations or iterations to run.</summary>
    public int Generations { get; init; } = 500;

    /// <summary>Gets or inits how many independent runs to perform for statistical validation.</summary>
    public int Repetitions { get; init; } = 10;

    /// <summary>
    /// Gets or inits the number of elite chromosomes copied unchanged into the next generation.
    /// Elitism prevents the best solution found from being lost during generational replacement.
    /// </summary>
    public int EliteCount { get; init; } = 2;

    /// <summary>
    /// Gets or inits an optional cancellation token that algorithms check at each generation
    /// boundary. When cancellation is requested the algorithm returns the best solution found
    /// so far rather than waiting for all generations to complete.
    /// Defaults to <see cref="CancellationToken.None"/> (no timeout).
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
}
