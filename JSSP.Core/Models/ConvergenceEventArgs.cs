namespace JSSP.Core.Models;

/// <summary>
/// Event data published by an algorithm after each generation or iteration.
/// </summary>
public class ConvergenceEventArgs : EventArgs
{
    /// <summary>Gets the current generation, iteration, or improvement counter.</summary>
    public int Generation { get; }

    /// <summary>Gets the best makespan found up to this generation.</summary>
    public int BestMakespan { get; }

    /// <summary>
    /// Gets the total number of iterations this algorithm will perform, or
    /// <c>-1</c> when the search is unbounded (e.g. Branch-and-Bound exhaustive DFS).
    /// The UI uses this to decide whether to show "Gen X / Y" or "Nodes: X".
    /// </summary>
    public int TotalIterations { get; }

    /// <summary>
    /// Initialises a new instance of <see cref="ConvergenceEventArgs"/>.
    /// </summary>
    /// <param name="generation">Current generation or improvement counter.</param>
    /// <param name="bestMakespan">Best makespan found so far.</param>
    /// <param name="totalIterations">
    /// Upper bound on iterations, or <c>-1</c> for unbounded algorithms.
    /// </param>
    public ConvergenceEventArgs(int generation, int bestMakespan, int totalIterations = -1)
    {
        Generation = generation;
        BestMakespan = bestMakespan;
        TotalIterations = totalIterations;
    }
}
