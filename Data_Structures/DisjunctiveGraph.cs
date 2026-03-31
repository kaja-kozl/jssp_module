using System.Runtime.CompilerServices;

public class DisjunctiveGraph // (G)raph, (V)erticies & (E)dges G(V,E) - DisjunctiveGraph(Operation,)
{
    // Adjacency List with a collection of Operations and Edges

    // Adding nodes
    // Adding conjunctive edges & Adding disjunctive edges
    // Removing edges
    // Querying neighbors
    
    // Cycle detection
    // Orientation of disjunctive edges
}

public class Edge
{
    
}

public enum EdgeType
{
    Conjunctive, // Directed (mandatory sequence)
    Disjunctive // Undirected (demonstrates optional / ordering constraints)
}