namespace JSSP.Core.Models;

/// <summary>
/// Immutable record of one algorithm's outcome within a "compare all" run.
/// <see cref="Stats"/> is null when the algorithm faulted; <see cref="ErrorMessage"/>
/// is null when it succeeded.
/// </summary>
public sealed class AlgorithmComparisonResult
{
    /// <summary>Gets the display name of the algorithm (as returned by <c>AlgorithmFactory</c>).</summary>
    public string AlgorithmName { get; }

    /// <summary>Gets the statistical results, or <see langword="null"/> if the run faulted.</summary>
    public AlgorithmStats? Stats { get; }

    /// <summary>Gets the error message, or <see langword="null"/> if the run succeeded.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Gets a value indicating whether this result represents a faulted run.</summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>
    /// Initialises a new <see cref="AlgorithmComparisonResult"/>.
    /// </summary>
    /// <param name="algorithmName">Display name of the algorithm.</param>
    /// <param name="stats">Stats from a successful run, or <see langword="null"/>.</param>
    /// <param name="errorMessage">Error message from a faulted run, or <see langword="null"/>.</param>
    public AlgorithmComparisonResult(string algorithmName, AlgorithmStats? stats, string? errorMessage)
    {
        AlgorithmName = algorithmName;
        Stats = stats;
        ErrorMessage = errorMessage;
    }
}
