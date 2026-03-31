public class Operation // Verticies, represent operations or tasks
{
    public int JobId { get; set; }
    public int OperationId { get; set; }
    public string Subdivision { get; set; }
    public int ProcessingTime { get; set; }

    public Operation(int jobId, int operationId, string subdivision, int processingTime)
    {
        JobId = jobId;
        OperationId = operationId;
        Subdivision = subdivision;
        ProcessingTime = processingTime;
    }
}