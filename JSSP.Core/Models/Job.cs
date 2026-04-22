namespace JSSP.Core.Models;

/// <summary>
/// Represents a job consisting of an ordered sequence of operations that must
/// be executed in the given order (conjunctive constraint).
/// </summary>
public class Job
{
    /// <summary>Gets the unique identifier for this job.</summary>
    public int Id { get; }

    /// <summary>Gets the ordered list of operations belonging to this job.</summary>
    public IReadOnlyList<Operation> Operations { get; }

    /// <summary>
    /// Initialises a new instance of <see cref="Job"/>.
    /// </summary>
    /// <param name="id">Unique job identifier.</param>
    /// <param name="operations">Ordered operations for this job.</param>
    public Job(int id, IReadOnlyList<Operation> operations)
    {
        Id = id;
        Operations = operations;
    }
}
