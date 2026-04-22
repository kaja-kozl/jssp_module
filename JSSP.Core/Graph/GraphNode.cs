using JSSP.Core.Models;

namespace JSSP.Core.Graph;

/// <summary>
/// Classifies a node in the disjunctive graph.
/// </summary>
public enum NodeKind
{
    /// <summary>
    /// Virtual source node — no associated operation.
    /// Has zero-weight conjunctive arcs to every job's first operation.
    /// </summary>
    Source,

    /// <summary>
    /// Virtual sink node — no associated operation.
    /// Every job's last operation has a conjunctive arc pointing here.
    /// </summary>
    Sink,

    /// <summary>A node representing a single schedulable operation.</summary>
    Operation
}

/// <summary>
/// A vertex in the disjunctive graph, representing either a schedulable operation
/// or one of the two virtual boundary nodes (source / sink).
/// </summary>
public class GraphNode
{
    /// <summary>
    /// Gets the unique integer identifier assigned during graph construction.
    /// Source = 0, Sink = 1, operation nodes start at 2.
    /// </summary>
    public int NodeId { get; }

    /// <summary>Gets the kind of this node (Source, Sink, or Operation).</summary>
    public NodeKind Kind { get; }

    /// <summary>
    /// Gets the operation this node represents, or <see langword="null"/> for
    /// the virtual source and sink nodes.
    /// </summary>
    public Operation? Operation { get; }

    /// <summary>
    /// Initialises a new node. Nodes are only constructed by
    /// <see cref="DisjunctiveGraph.Build"/>.
    /// </summary>
    /// <param name="nodeId">Unique identifier within the owning graph.</param>
    /// <param name="kind">Source, Sink, or Operation.</param>
    /// <param name="operation">
    /// The operation this node wraps; <see langword="null"/> for Source/Sink nodes.
    /// </param>
    internal GraphNode(int nodeId, NodeKind kind, Operation? operation)
    {
        NodeId = nodeId;
        Kind = kind;
        Operation = operation;
    }
}
