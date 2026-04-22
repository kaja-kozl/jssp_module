namespace JSSP.IO;

/// <summary>
/// A single data point in an algorithm's convergence curve: the best makespan
/// observed at a given generation.
/// </summary>
/// <param name="Generation">The generation or iteration index (1-based).</param>
/// <param name="BestMakespan">The best makespan found up to and including this generation.</param>
public sealed record ConvergencePoint(int Generation, int BestMakespan);
