using JSSP.Core.Models;

namespace JSSP.Core.Graph;

/// <summary>
/// Disjunctive graph G(V, E) representing a JSSP instance.
/// <para>
/// Vertices are operation nodes plus virtual <see cref="Source"/> and
/// <see cref="Sink"/> nodes. Conjunctive arcs enforce job-order precedence (fixed,
/// never change after construction). Disjunctive arc pairs represent machine
/// contention; exactly one direction per pair is active after
/// <see cref="Orient"/> is called.
/// </para>
/// <para>
/// <see cref="ReverseArc"/> flips one disjunctive arc for Tabu N7 neighbourhood
/// evaluation. Call it twice (forward then backward) to apply and undo a move.
/// </para>
/// </summary>
public class DisjunctiveGraph
{
    // Fixed conjunctive successors, keyed by source node. Never mutated after Build().
    private readonly Dictionary<GraphNode, List<GraphEdge>> _conjSuccessors;

    // Currently-oriented disjunctive successors. Mutated by Orient() and ReverseArc().
    private readonly Dictionary<GraphNode, List<GraphEdge>> _disjSuccessors;

    // Unoriented disjunctive pairs — stored with A.NodeId < B.NodeId for determinism.
    // Used by Orient() to rebuild orientation from a schedule without rebuilding the graph.
    private readonly List<(GraphNode A, GraphNode B)> _disjunctivePairs;

    // (minNodeId, maxNodeId) set for O(1) AreDisjunctiveNeighbours lookup.
    private readonly HashSet<(int Min, int Max)> _disjunctivePairSet;

    // All other operation nodes on the same machine, keyed by node.
    private readonly Dictionary<GraphNode, List<GraphNode>> _machinePeers;

    // Flat ordered list [Source, Sink, ...OperationNodes] for internal iteration.
    private readonly IReadOnlyList<GraphNode> _allNodes;

    /// <summary>
    /// Gets the virtual source node. Has zero-weight conjunctive arcs to every
    /// job's first operation; no disjunctive arcs.
    /// </summary>
    public GraphNode Source { get; }

    /// <summary>
    /// Gets the virtual sink node. Every job's last operation has a conjunctive
    /// arc pointing here; no disjunctive arcs.
    /// </summary>
    public GraphNode Sink { get; }

    /// <summary>Gets all operation nodes in the graph.</summary>
    public IReadOnlyList<GraphNode> OperationNodes { get; }

    /// <summary>
    /// All nodes: Source, Sink, and every operation node.
    /// Exposed as internal for <see cref="CriticalPathFinder"/>.
    /// </summary>
    internal IReadOnlyList<GraphNode> AllNodes => _allNodes;

    private DisjunctiveGraph(
        GraphNode source,
        GraphNode sink,
        IReadOnlyList<GraphNode> operationNodes,
        Dictionary<GraphNode, List<GraphEdge>> conjSuccessors,
        Dictionary<GraphNode, List<GraphEdge>> disjSuccessors,
        List<(GraphNode A, GraphNode B)> disjunctivePairs,
        HashSet<(int Min, int Max)> disjunctivePairSet,
        Dictionary<GraphNode, List<GraphNode>> machinePeers,
        IReadOnlyList<GraphNode> allNodes)
    {
        Source = source;
        Sink = sink;
        OperationNodes = operationNodes;
        _conjSuccessors = conjSuccessors;
        _disjSuccessors = disjSuccessors;
        _disjunctivePairs = disjunctivePairs;
        _disjunctivePairSet = disjunctivePairSet;
        _machinePeers = machinePeers;
        _allNodes = allNodes;
    }

    /// <summary>
    /// Builds the disjunctive graph for the given jobs.
    /// Conjunctive arcs are fixed; disjunctive arcs are unoriented until
    /// <see cref="Orient"/> is called.
    /// </summary>
    /// <param name="jobs">The jobs to model. May be empty.</param>
    /// <returns>A fully constructed <see cref="DisjunctiveGraph"/>.</returns>
    public static DisjunctiveGraph Build(IReadOnlyList<Job> jobs)
    {
        int nextId = 0;
        var source = new GraphNode(nextId++, NodeKind.Source, null);
        var sink = new GraphNode(nextId++, NodeKind.Sink, null);

        var opNodes = new List<GraphNode>();
        var nodeMap = new Dictionary<(int JobId, int OpId), GraphNode>();

        foreach (var job in jobs)
        {
            foreach (var op in job.Operations)
            {
                var node = new GraphNode(nextId++, NodeKind.Operation, op);
                opNodes.Add(node);
                nodeMap[(op.JobId, op.OperationId)] = node;
            }
        }

        var allNodesList = new List<GraphNode>(opNodes.Count + 2) { source, sink };
        allNodesList.AddRange(opNodes);
        IReadOnlyList<GraphNode> allNodes = allNodesList.AsReadOnly();

        var conjSucc = new Dictionary<GraphNode, List<GraphEdge>>(allNodesList.Count);
        var disjSucc = new Dictionary<GraphNode, List<GraphEdge>>(allNodesList.Count);
        var machinePeers = new Dictionary<GraphNode, List<GraphNode>>(allNodesList.Count);

        foreach (var n in allNodesList)
        {
            conjSucc[n] = new List<GraphEdge>();
            disjSucc[n] = new List<GraphEdge>();
            machinePeers[n] = new List<GraphNode>();
        }

        // Conjunctive arcs: Source → first op → ... → last op → Sink, per job.
        foreach (var job in jobs)
        {
            var ops = job.Operations;
            if (ops.Count == 0) continue;

            var firstNode = nodeMap[(job.Id, ops[0].OperationId)];
            conjSucc[source].Add(new GraphEdge(EdgeType.Conjunctive, firstNode, 0));

            for (int i = 0; i < ops.Count - 1; i++)
            {
                var from = nodeMap[(ops[i].JobId, ops[i].OperationId)];
                var to = nodeMap[(ops[i + 1].JobId, ops[i + 1].OperationId)];
                conjSucc[from].Add(new GraphEdge(EdgeType.Conjunctive, to, ops[i].ProcessingTime));
            }

            var lastOp = ops[ops.Count - 1];
            var lastNode = nodeMap[(lastOp.JobId, lastOp.OperationId)];
            conjSucc[lastNode].Add(new GraphEdge(EdgeType.Conjunctive, sink, lastOp.ProcessingTime));
        }

        // Disjunctive pairs: group operation nodes by machine, then pair all on same machine.
        var byMachine = new Dictionary<string, List<GraphNode>>();
        foreach (var node in opNodes)
        {
            // node.Operation is non-null: all nodes in opNodes have Kind == Operation.
            var machine = node.Operation!.Subdivision;
            if (!byMachine.TryGetValue(machine, out var peers))
            {
                peers = new List<GraphNode>();
                byMachine[machine] = peers;
            }
            peers.Add(node);
        }

        var disjPairs = new List<(GraphNode A, GraphNode B)>();
        var disjPairSet = new HashSet<(int Min, int Max)>();

        foreach (var (_, peers) in byMachine)
        {
            for (int i = 0; i < peers.Count; i++)
            {
                for (int j = i + 1; j < peers.Count; j++)
                {
                    var a = peers[i];
                    var b = peers[j];

                    // Normalise: A always has the smaller NodeId for deterministic orientation.
                    var (pa, pb) = a.NodeId < b.NodeId ? (a, b) : (b, a);
                    disjPairs.Add((pa, pb));
                    disjPairSet.Add((pa.NodeId, pb.NodeId));

                    machinePeers[a].Add(b);
                    machinePeers[b].Add(a);
                }
            }
        }

        return new DisjunctiveGraph(
            source, sink, opNodes.AsReadOnly(),
            conjSucc, disjSucc,
            disjPairs, disjPairSet,
            machinePeers, allNodes);
    }

    /// <summary>
    /// Orients all disjunctive pairs from <paramref name="schedule"/> start times.
    /// If operation A starts no later than B on the same machine, arc A→B is selected
    /// (ties broken by ascending <see cref="GraphNode.NodeId"/>).
    /// Replaces any previous orientation.
    /// </summary>
    /// <param name="schedule">Schedule supplying start times for all operations.</param>
    public void Orient(Schedule schedule)
    {
        foreach (var node in _allNodes)
            _disjSuccessors[node].Clear();

        foreach (var (a, b) in _disjunctivePairs)
        {
            // a.Operation and b.Operation are non-null: disjunctive pairs are always operation nodes.
            int startA = schedule.GetStartTime(a.Operation!.JobId, a.Operation.OperationId);
            int startB = schedule.GetStartTime(b.Operation!.JobId, b.Operation.OperationId);

            if (startA <= startB)
                _disjSuccessors[a].Add(new GraphEdge(EdgeType.Disjunctive, b, a.Operation.ProcessingTime));
            else
                _disjSuccessors[b].Add(new GraphEdge(EdgeType.Disjunctive, a, b.Operation.ProcessingTime));
        }
    }

    /// <summary>
    /// Reverses the currently-oriented disjunctive arc from <paramref name="from"/>
    /// to <paramref name="to"/>, replacing it with the arc
    /// <paramref name="to"/>→<paramref name="from"/>.
    /// </summary>
    /// <remarks>
    /// Used by Tabu search to apply and undo N7 neighbourhood moves.
    /// Call <c>ReverseArc(a, b)</c> to apply a move and
    /// <c>ReverseArc(b, a)</c> to undo it.
    /// </remarks>
    /// <param name="from">Current source of the disjunctive arc.</param>
    /// <param name="to">Current destination of the disjunctive arc.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when no oriented disjunctive arc from <paramref name="from"/> to
    /// <paramref name="to"/> exists.
    /// </exception>
    public void ReverseArc(GraphNode from, GraphNode to)
    {
        var list = _disjSuccessors[from];
        int idx = list.FindIndex(e => e.To == to);
        if (idx < 0)
            throw new ArgumentException(
                $"No oriented disjunctive arc from node {from.NodeId} to node {to.NodeId}.");

        list.RemoveAt(idx);

        // to.Operation is non-null: ReverseArc is only valid between operation nodes.
        _disjSuccessors[to].Add(new GraphEdge(EdgeType.Disjunctive, from, to.Operation!.ProcessingTime));
    }

    /// <summary>
    /// Returns all outgoing arcs from <paramref name="node"/>: fixed conjunctive arcs
    /// plus any currently-oriented disjunctive arcs.
    /// </summary>
    /// <param name="node">The source node.</param>
    /// <returns>Combined read-only list of outgoing arcs.</returns>
    public IReadOnlyList<GraphEdge> GetSuccessors(GraphNode node)
    {
        var conj = _conjSuccessors[node];
        var disj = _disjSuccessors[node];

        if (disj.Count == 0)
            return conj.AsReadOnly();

        var combined = new List<GraphEdge>(conj.Count + disj.Count);
        combined.AddRange(conj);
        combined.AddRange(disj);
        return combined.AsReadOnly();
    }

    /// <summary>
    /// Returns all operation nodes that share the same machine as
    /// <paramref name="node"/>. Returns an empty list for the virtual source
    /// and sink nodes.
    /// </summary>
    /// <param name="node">The node whose machine peers are requested.</param>
    public IReadOnlyList<GraphNode> GetMachinePeers(GraphNode node) =>
        _machinePeers[node].AsReadOnly();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="a"/> and
    /// <paramref name="b"/> are a disjunctive pair — i.e. they compete for
    /// the same machine.
    /// </summary>
    /// <param name="a">First node.</param>
    /// <param name="b">Second node.</param>
    public bool AreDisjunctiveNeighbours(GraphNode a, GraphNode b)
    {
        int lo = Math.Min(a.NodeId, b.NodeId);
        int hi = Math.Max(a.NodeId, b.NodeId);
        return _disjunctivePairSet.Contains((lo, hi));
    }
}
