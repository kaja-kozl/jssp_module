namespace JSSP.Core.Models;

/// <summary>
/// Statistical summary produced after completing all repetitions of an algorithm run.
/// </summary>
public class AlgorithmStats
{
    /// <summary>Gets the best makespan found across all repetitions.</summary>
    public int BestMakespan { get; init; }

    /// <summary>Gets the mean makespan across all repetitions.</summary>
    public double MeanMakespan { get; init; }

    /// <summary>Gets the standard deviation of makespans across all repetitions.</summary>
    public double StdDev { get; init; }

    /// <summary>Gets the generation at which the best solution was first found.</summary>
    public int ConvergenceGeneration { get; init; }

    /// <summary>Gets the wall-clock time taken by the full <c>Solve</c> call.</summary>
    public TimeSpan ElapsedTime { get; init; }
}
