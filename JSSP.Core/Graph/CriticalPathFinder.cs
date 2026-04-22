using JSSP.Core.Models;

namespace JSSP.Core.Graph;

/// <summary>
/// Computes the critical path (longest weighted path from source to sink) in an
/// oriented disjunctive graph using topological-sort dynamic programming — O(V + E).
/// <para>
/// All public and internal methods are stateless: all working state is allocated
/// locally per call, so the same <see cref="DisjunctiveGraph"/> may be analysed
/// repeatedly without data races between calls.
/// </para>
/// <para>
/// The critical path must be recomputed after every Tabu neighbourhood move.
/// The result must not be cached across <see cref="DisjunctiveGraph.ReverseArc"/> calls.
/// </para>
/// </summary>
public class CriticalPathFinder
{
    /// <summary>
    /// Returns the makespan of the oriented graph — i.e. the length of the
    /// critical path from <see cref="DisjunctiveGraph.Source"/> to
    /// <see cref="DisjunctiveGraph.Sink"/>.
    /// </summary>
    /// <param name="graph">The oriented disjunctive graph to analyse.</param>
    /// <returns>The makespan: the longest weighted path from source to sink.</returns>
    public int FindMakespan(DisjunctiveGraph graph)
    {
        var (makespan, _, _, _) = Compute(graph);
        return makespan;
    }

    /// <summary>
    /// Returns the ordered sequence of nodes forming the critical path, from
    /// <see cref="DisjunctiveGraph.Source"/> to <see cref="DisjunctiveGraph.Sink"/>.
    /// </summary>
    /// <param name="graph">The oriented disjunctive graph to analyse.</param>
    /// <returns>
    /// Nodes on the critical path in traversal order, starting with Source and
    /// ending with Sink.
    /// </returns>
    public IReadOnlyList<GraphNode> Find(DisjunctiveGraph graph)
    {
        var (_, predecessors, _, _) = Compute(graph);
        return BacktrackPath(graph.Source, graph.Sink, predecessors);
    }

    /// <summary>
    /// Attempts to compute the makespan of the oriented graph. Returns
    /// <see langword="false"/> when the graph contains a cycle (caused by an
    /// invalid Tabu neighbourhood move), in which case <paramref name="makespan"/>
    /// is set to <c>-1</c>.
    /// </summary>
    /// <param name="graph">The oriented disjunctive graph to analyse.</param>
    /// <param name="makespan">
    /// The makespan when the graph is acyclic; <c>-1</c> if a cycle is detected.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the graph is acyclic and the makespan is valid;
    /// <see langword="false"/> if a cycle was detected.
    /// </returns>
    public bool TryFindMakespan(DisjunctiveGraph graph, out int makespan)
    {
        var (ms, _, _, processedCount) = Compute(graph);
        bool acyclic = processedCount == graph.AllNodes.Count;
        makespan = acyclic ? ms : -1;
        return acyclic;
    }

    /// <summary>
    /// Builds a <see cref="Schedule"/> from the current orientation of
    /// <paramref name="graph"/> by assigning each operation node its earliest
    /// start time, computed via longest-path forward pass.
    /// </summary>
    /// <remarks>
    /// Used by Tabu search after applying each neighbourhood move to obtain the
    /// updated schedule without re-running the full active decoder.
    /// Callers are responsible for ensuring the graph is acyclic before calling
    /// (e.g., by calling <see cref="TryFindMakespan"/> first).
    /// </remarks>
    /// <param name="graph">An acyclic, fully-oriented disjunctive graph.</param>
    /// <returns>
    /// A <see cref="Schedule"/> with start times for every operation node.
    /// </returns>
    public Schedule FindSchedule(DisjunctiveGraph graph)
    {
        var (_, _, dist, _) = Compute(graph);
        var schedule = new Schedule();
        foreach (var node in graph.OperationNodes)
        {
            // node.Operation is non-null: OperationNodes only contains Operation-kind nodes.
            var op = node.Operation!;
            schedule.SetStartTime(op.JobId, op.OperationId, dist[node], op.ProcessingTime);
        }
        return schedule;
    }

    /// <summary>
    /// Runs Kahn's topological sort with a simultaneous forward longest-path DP.
    /// <para>
    /// For each node u dequeued in topological order, every successor v is relaxed:
    /// <c>dist[v] = max(dist[v], dist[u] + weight(u→v))</c>. The predecessor map
    /// records which u achieved the maximum for path reconstruction.
    /// </para>
    /// </summary>
    /// <param name="graph">The graph to compute on.</param>
    /// <returns>
    /// The makespan (dist[Sink]), a predecessor map for path reconstruction,
    /// start-time distances for all nodes, and the count of nodes successfully
    /// processed by the topological sort. If <c>ProcessedCount &lt; AllNodes.Count</c>
    /// the graph contains a cycle.
    /// </returns>
    private static (
        int Makespan,
        Dictionary<GraphNode, GraphNode?> Predecessors,
        Dictionary<GraphNode, int> Dist,
        int ProcessedCount) Compute(DisjunctiveGraph graph)
    {
        var allNodes = graph.AllNodes;
        int nodeCount = allNodes.Count;

        var dist = new Dictionary<GraphNode, int>(nodeCount);
        var inDegree = new Dictionary<GraphNode, int>(nodeCount);
        var predecessors = new Dictionary<GraphNode, GraphNode?>(nodeCount);

        foreach (var node in allNodes)
        {
            dist[node] = 0;
            inDegree[node] = 0;
            predecessors[node] = null;
        }

        // Compute in-degrees by sweeping all successor lists.
        foreach (var node in allNodes)
        {
            foreach (var edge in graph.GetSuccessors(node))
                inDegree[edge.To]++;
        }

        // Seed Kahn's queue with all zero-in-degree nodes (Source in a valid graph).
        var queue = new Queue<GraphNode>();
        foreach (var node in allNodes)
        {
            if (inDegree[node] == 0)
                queue.Enqueue(node);
        }

        // Process nodes in topological order; relax each outgoing arc.
        int processedCount = 0;
        while (queue.Count > 0)
        {
            var u = queue.Dequeue();
            processedCount++;
            foreach (var edge in graph.GetSuccessors(u))
            {
                var v = edge.To;
                int candidate = dist[u] + edge.Weight;
                if (candidate > dist[v])
                {
                    dist[v] = candidate;
                    predecessors[v] = u;
                }
                else if (candidate == dist[v] && predecessors[v] == null)
                {
                    // Zero-weight arc from Source: record predecessor so BacktrackPath
                    // can reach Source during path reconstruction.
                    predecessors[v] = u;
                }
                if (--inDegree[v] == 0)
                    queue.Enqueue(v);
            }
        }

        return (dist[graph.Sink], predecessors, dist, processedCount);
    }

    /// <summary>
    /// Reconstructs the critical path by walking predecessor links from
    /// <paramref name="sink"/> back to <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source node (walk terminates here).</param>
    /// <param name="sink">The sink node (walk starts here).</param>
    /// <param name="predecessors">Predecessor map produced by <see cref="Compute"/>.</param>
    /// <returns>Nodes in source-to-sink order.</returns>
    private static IReadOnlyList<GraphNode> BacktrackPath(
        GraphNode source,
        GraphNode sink,
        Dictionary<GraphNode, GraphNode?> predecessors)
    {
        var path = new List<GraphNode>();
        GraphNode? current = sink;

        while (current != null)
        {
            path.Add(current);
            if (current == source) break;
            predecessors.TryGetValue(current, out current);
        }

        path.Reverse();
        return path.AsReadOnly();
    }
}
