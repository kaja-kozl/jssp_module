namespace JSSP.Core.Models;

/// <summary>
/// Represents a feasible schedule that maps each operation to a start time.
/// The makespan is the maximum completion time across all operations.
/// </summary>
public class Schedule
{
    private readonly Dictionary<(int jobId, int operationId), int> _startTimes = new();

    /// <summary>Gets the makespan (length) of this schedule.</summary>
    public int Makespan { get; private set; }

    /// <summary>
    /// Records the start time for a given operation and updates the makespan.
    /// </summary>
    /// <param name="jobId">Job identifier.</param>
    /// <param name="operationId">Operation identifier within the job.</param>
    /// <param name="startTime">Start time to assign.</param>
    /// <param name="processingTime">Duration of the operation.</param>
    public void SetStartTime(int jobId, int operationId, int startTime, int processingTime)
    {
        _startTimes[(jobId, operationId)] = startTime;
        int completionTime = startTime + processingTime;
        if (completionTime > Makespan)
            Makespan = completionTime;
    }

    /// <summary>Returns the assigned start time for the given operation, or 0 if unset.</summary>
    public int GetStartTime(int jobId, int operationId) =>
        _startTimes.TryGetValue((jobId, operationId), out int t) ? t : 0;

    /// <summary>
    /// Returns <see langword="true"/> when a start time has been explicitly recorded
    /// for the given operation.
    /// </summary>
    /// <remarks>
    /// Needed by <see cref="JSSP.Core.Validation.ScheduleValidator"/> to distinguish
    /// an operation that was intentionally assigned start time 0 from one that was
    /// never added to the schedule (both return 0 from <see cref="GetStartTime"/>).
    /// </remarks>
    /// <param name="jobId">Job identifier.</param>
    /// <param name="operationId">Operation identifier within the job.</param>
    public bool ContainsOperation(int jobId, int operationId) =>
        _startTimes.ContainsKey((jobId, operationId));
}
