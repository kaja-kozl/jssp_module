namespace JSSP.Core.Models;

/// <summary>
/// Represents a machine that processes operations.
/// Each machine can process at most one operation at a time (hard constraint 2).
/// </summary>
public class Machine
{
    /// <summary>Gets the unique identifier for this machine.</summary>
    public int Id { get; }

    /// <summary>Gets the name of this machine.</summary>
    public string Name { get; }

    /// <summary>
    /// Initialises a new instance of <see cref="Machine"/>.
    /// </summary>
    public Machine(int id, string name)
    {
        Id = id;
        Name = name;
    }
}
