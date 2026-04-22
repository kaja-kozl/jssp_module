namespace JSSP.Algorithms.Tabu;

/// <summary>
/// Represents a forbidden move in the Tabu list.
/// A move (FromId → ToId) is recorded as tabu after the search applies the
/// reverse swap — preventing immediate cycling back to a previously visited state.
/// </summary>
/// <remarks>
/// Node IDs are used rather than <c>GraphNode</c> references so the struct is a
/// pure value type: two <see cref="TabuMove"/> instances with the same IDs compare
/// as equal without reference equality.
/// </remarks>
/// <param name="FromId">NodeId of the source node in the forbidden arc.</param>
/// <param name="ToId">NodeId of the destination node in the forbidden arc.</param>
internal readonly record struct TabuMove(int FromId, int ToId);
