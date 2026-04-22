namespace JSSP.Core.Models;

/// <summary>
/// Represents a single operation (vertex) in the disjunctive graph.
/// An operation belongs to exactly one job and must be processed on a specific machine.
/// </summary>
public class Operation
{
    /// <summary>Gets the identifier of the job this operation belongs to.</summary>
    public int JobId { get; }

    /// <summary>Gets the position of this operation within its job's sequence.</summary>
    public int OperationId { get; }

    /// <summary>Gets the name of the machine (subdivision) that processes this operation.</summary>
    public string Subdivision { get; }

    /// <summary>Gets the processing time required to complete this operation.</summary>
    public int ProcessingTime { get; }

    /// <summary>
    /// Initialises a new instance of <see cref="Operation"/>.
    /// </summary>
    public Operation(int jobId, int operationId, string subdivision, int processingTime)
    {
        JobId = jobId;
        OperationId = operationId;
        Subdivision = subdivision;
        ProcessingTime = processingTime;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"JobId: {JobId}, OperationId: {OperationId}, " +
        $"Subdivision: {Subdivision}, ProcessingTime: {ProcessingTime}";
}
