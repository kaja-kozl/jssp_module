using JSSP.Core.Models;

namespace JSSP.IO;

/// <summary>
/// Loads JSSP job data from a CSV file.
/// Expected columns: JobId, OperationId, Subdivision, ProcessingTime (header row is skipped).
/// Malformed rows are skipped silently to match the resilience of the original parser.
/// </summary>
public static class CsvLoader
{
    /// <summary>Minimum valid processing time (inclusive).</summary>
    private const int MinProcessingTime = 1;

    /// <summary>Minimum valid job or operation identifier (inclusive).</summary>
    private const int MinId = 1;

    // -------------------------------------------------------------------------
    // Synchronous API (kept for backward compatibility with existing callers)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads all jobs from the given CSV file path.
    /// </summary>
    /// <param name="path">Absolute or relative path to the CSV file.</param>
    /// <returns>
    /// List of <see cref="Job"/> objects ordered by job ID, each containing their
    /// ordered operations.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static List<Job> Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("CSV file not found.", path);

        var operationsByJob = new Dictionary<int, List<Operation>>();

        using var reader = new StreamReader(path);
        reader.ReadLine(); // skip header

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var parts = line.Split(',');
            if (parts.Length < 4) continue;

            try
            {
                int jobId = int.Parse(parts[0].Trim());
                int operationId = int.Parse(parts[1].Trim());
                string subdivision = parts[2].Trim();
                int processingTime = int.Parse(parts[3].Trim());

                if (!operationsByJob.ContainsKey(jobId))
                    operationsByJob[jobId] = new List<Operation>();

                operationsByJob[jobId].Add(new Operation(jobId, operationId, subdivision, processingTime));
            }
            catch (FormatException)
            {
                // skip malformed rows
            }
        }

        return operationsByJob
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new Job(kvp.Key, kvp.Value.AsReadOnly()))
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Async API with full dataset validation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asynchronously loads all jobs from the given CSV file path,
    /// validating each row for content correctness beyond basic parse errors.
    /// </summary>
    /// <remarks>
    /// Rows are rejected (and a warning added) when:
    /// <list type="bullet">
    ///   <item>fewer than four comma-separated fields are present</item>
    ///   <item>JobId or OperationId is not a positive integer</item>
    ///   <item>ProcessingTime is not a positive integer (&gt;= 1)</item>
    ///   <item>Subdivision is null or whitespace</item>
    ///   <item>the (JobId, OperationId) pair has already been seen (duplicate)</item>
    /// </list>
    /// The file is read with BOM detection enabled so UTF-8 BOM headers are
    /// handled transparently.
    /// </remarks>
    /// <param name="path">Absolute or relative path to the CSV file.</param>
    /// <param name="cancellationToken">Token that cancels the read mid-stream.</param>
    /// <returns>
    /// A <see cref="CsvLoadResult"/> containing the parsed jobs (ordered by job ID)
    /// and any per-row warning messages.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public static async Task<CsvLoadResult> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("CSV file not found.", path);

        var operationsByJob = new Dictionary<int, List<Operation>>();
        var seen = new HashSet<(int jobId, int operationId)>();
        var warnings = new List<string>();

        // detectEncodingFromByteOrderMarks: true handles UTF-8 BOM transparently.
        using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);

        // Skip header row.
        await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        int lineNumber = 1; // header was line 1; data rows start at 2
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            lineNumber++;
            cancellationToken.ThrowIfCancellationRequested();

            string? warning = TryParseLine(line, lineNumber, seen, out Operation? operation);

            if (warning != null)
            {
                warnings.Add(warning);
                continue;
            }

            // operation is non-null whenever warning is null.
            Operation op = operation!;

            if (!operationsByJob.ContainsKey(op.JobId))
                operationsByJob[op.JobId] = [];

            operationsByJob[op.JobId].Add(op);
        }

        var jobs = operationsByJob
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new Job(kvp.Key, kvp.Value.AsReadOnly()))
            .ToList();

        return new CsvLoadResult(jobs, warnings.AsReadOnly());
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to parse a single CSV data line into an <see cref="Operation"/>.
    /// </summary>
    /// <param name="line">Raw CSV line (no newline character).</param>
    /// <param name="lineNumber">1-based line number in the file (used in warning text).</param>
    /// <param name="seen">
    /// Set of (jobId, operationId) pairs already accepted; updated on success.
    /// </param>
    /// <param name="operation">
    /// Set to the parsed <see cref="Operation"/> on success; <see langword="null"/> on failure.
    /// </param>
    /// <returns>
    /// <see langword="null"/> on success; a human-readable warning string on failure.
    /// </returns>
    private static string? TryParseLine(
        string line,
        int lineNumber,
        HashSet<(int, int)> seen,
        out Operation? operation)
    {
        operation = null;

        var parts = line.Split(',');
        if (parts.Length < 4)
            return $"Line {lineNumber}: expected 4 columns, found {parts.Length} — row skipped.";

        if (!int.TryParse(parts[0].Trim(), out int jobId) || jobId < MinId)
            return $"Line {lineNumber}: JobId '{parts[0].Trim()}' is not a positive integer — row skipped.";

        if (!int.TryParse(parts[1].Trim(), out int operationId) || operationId < MinId)
            return $"Line {lineNumber}: OperationId '{parts[1].Trim()}' is not a positive integer — row skipped.";

        string subdivision = parts[2].Trim();
        if (string.IsNullOrWhiteSpace(subdivision))
            return $"Line {lineNumber}: Subdivision is blank — row skipped.";

        if (!int.TryParse(parts[3].Trim(), out int processingTime) || processingTime < MinProcessingTime)
            return $"Line {lineNumber}: ProcessingTime '{parts[3].Trim()}' must be a positive integer >= {MinProcessingTime} — row skipped.";

        var key = (jobId, operationId);
        if (!seen.Add(key))
            return $"Line {lineNumber}: duplicate (JobId={jobId}, OperationId={operationId}) — row skipped.";

        operation = new Operation(jobId, operationId, subdivision, processingTime);
        return null;
    }
}
