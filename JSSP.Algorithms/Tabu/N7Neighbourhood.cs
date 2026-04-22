using JSSP.Core.Graph;

namespace JSSP.Algorithms.Tabu;

/// <summary>
/// Generates N7 neighbourhood moves for Tabu search on the JSSP disjunctive graph.
/// </summary>
/// <remarks>
/// <para>
/// The N7 neighbourhood (Dell'Amico &amp; Trubian, 1993) considers pairs of
/// consecutive operation nodes on the critical path that are connected by a
/// <em>disjunctive</em> arc — meaning they compete for the same machine and their
/// execution order has been fixed by the current schedule orientation.
/// Reversing such an arc (swapping the execution order of the two operations on
/// that machine) constitutes one N7 move.
/// </para>
/// <para>
/// Only disjunctive-arc pairs are returned. Conjunctive-arc pairs (operations from
/// the same job) cannot be swapped: their order is fixed by the job's precedence
/// constraint and reversing their arc would create an infeasible schedule.
/// </para>
/// <para>
/// Block-move mode (reversing a sub-sequence of operations between two bottleneck
/// points) is deferred to a future iteration; this implementation provides
/// single-swap moves only.
/// </para>
/// </remarks>
internal static class N7Neighbourhood
{
    /// <summary>
    /// Generates candidate moves from the current critical path.
    /// Each move is a pair of consecutive critical-path operation nodes that share
    /// a disjunctive arc — i.e. they are on the same machine and adjacent in the
    /// current schedule.
    /// </summary>
    /// <param name="criticalPath">
    /// The critical path as returned by <see cref="JSSP.Core.Graph.CriticalPathFinder.Find"/>,
    /// in source-to-sink order. The first element is the virtual source node and
    /// the last is the virtual sink node.
    /// </param>
    /// <param name="graph">
    /// The oriented disjunctive graph. Used to test whether adjacent critical-path
    /// nodes share a disjunctive arc via
    /// <see cref="DisjunctiveGraph.AreDisjunctiveNeighbours"/>.
    /// </param>
    /// <returns>
    /// A read-only list of (From, To) node pairs where the arc From→To is currently
    /// oriented and can be reversed as a Tabu move. Returns an empty list when no
    /// eligible swap exists (e.g. the critical path contains only virtual nodes, or
    /// all adjacent pairs are connected by conjunctive arcs).
    /// </returns>
    public static IReadOnlyList<(GraphNode From, GraphNode To)> Generate(
        IReadOnlyList<GraphNode> criticalPath,
        DisjunctiveGraph graph)
    {
        var moves = new List<(GraphNode From, GraphNode To)>();

        for (int i = 0; i < criticalPath.Count - 1; i++)
        {
            var u = criticalPath[i];
            var v = criticalPath[i + 1];

            // Only operation nodes can be swapped; skip Source, Sink, and
            // any conjunctive-arc pairs (same-job, fixed order).
            if (u.Kind != NodeKind.Operation || v.Kind != NodeKind.Operation)
                continue;

            if (graph.AreDisjunctiveNeighbours(u, v))
                moves.Add((u, v));
        }

        return moves.AsReadOnly();
    }
}
