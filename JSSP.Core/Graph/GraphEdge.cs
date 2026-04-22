namespace JSSP.Core.Graph;

/// <summary>
/// A directed arc in the disjunctive graph.
/// </summary>
public class GraphEdge
{
    /// <summary>Gets the type of this arc (conjunctive or disjunctive).</summary>
    public EdgeType Type { get; }

    /// <summary>Gets the destination node of this arc.</summary>
    public GraphNode To { get; }

    /// <summary>
    /// Gets the weight of this arc, equal to the processing time of the source
    /// node's operation.  For arcs leaving the virtual source node the weight is 0.
    /// </summary>
    public int Weight { get; }

    /// <summary>
    /// Initialises a new instance of <see cref="GraphEdge"/>. Edges are only
    /// constructed by <see cref="DisjunctiveGraph"/>.
    /// </summary>
    /// <param name="type">Conjunctive or disjunctive.</param>
    /// <param name="to">The destination node.</param>
    /// <param name="weight">
    /// Processing time of the source node's operation (0 for arcs from the
    /// virtual source).
    /// </param>
    internal GraphEdge(EdgeType type, GraphNode to, int weight)
    {
        Type = type;
        To = to;
        Weight = weight;
    }
}

/// <summary>
/// Classifies an arc in the disjunctive graph.
/// </summary>
public enum EdgeType
{
    /// <summary>Directed arc enforcing the mandatory operation sequence within a job.</summary>
    Conjunctive,

    /// <summary>
    /// Directed arc representing a resolved machine-resource conflict.
    /// Direction is set by <see cref="DisjunctiveGraph.Orient"/> based on
    /// schedule start times and may be flipped by
    /// <see cref="DisjunctiveGraph.ReverseArc"/> during Tabu neighbourhood moves.
    /// </summary>
    Disjunctive
}
