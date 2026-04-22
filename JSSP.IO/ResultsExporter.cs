using System.Text;
using System.Text.Json;
using JSSP.Core.Models;

namespace JSSP.IO;

/// <summary>
/// Exports algorithm results to various formats: CSV schedule table, JSON summary,
/// convergence curve CSV, and a plain-text Gantt table for display.
/// All write methods are async so that file I/O never blocks the UI thread.
/// </summary>
public static class ResultsExporter
{
    // -------------------------------------------------------------------------
    // CSV — Gantt-style schedule table
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asynchronously exports a Gantt-style schedule table and summary statistics
    /// to a CSV file.
    /// </summary>
    /// <remarks>
    /// Output columns: Machine, JobId, OperationId, StartTime, ProcessingTime, EndTime.
    /// Rows are sorted by machine name, then by start time.
    /// </remarks>
    /// <param name="stats">Statistical summary to append after the schedule rows.</param>
    /// <param name="schedule">The schedule to export.</param>
    /// <param name="jobs">Jobs used to resolve operation details.</param>
    /// <param name="path">Destination file path.</param>
    /// <param name="cancellationToken">Token that cancels the write.</param>
    public static async Task ExportCsvAsync(
        AlgorithmStats stats,
        Schedule schedule,
        List<Job> jobs,
        string path,
        CancellationToken cancellationToken = default)
    {
        var rows = BuildScheduleRows(schedule, jobs);
        rows.Sort((a, b) =>
            string.Compare(a.Machine, b.Machine, StringComparison.Ordinal) != 0
                ? string.Compare(a.Machine, b.Machine, StringComparison.Ordinal)
                : a.Start.CompareTo(b.Start));

        await using var writer = new StreamWriter(path, append: false, Encoding.UTF8);

        // Schedule table
        await writer.WriteLineAsync("Machine,JobId,OperationId,StartTime,ProcessingTime,EndTime")
            .ConfigureAwait(false);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(
                $"{row.Machine},{row.JobId},{row.OperationId},{row.Start},{row.Duration},{row.End}")
                .ConfigureAwait(false);
        }

        // Summary section
        await writer.WriteLineAsync("").ConfigureAwait(false);
        await writer.WriteLineAsync("Stat,Value").ConfigureAwait(false);
        await writer.WriteLineAsync($"BestMakespan,{stats.BestMakespan}").ConfigureAwait(false);
        await writer.WriteLineAsync($"MeanMakespan,{stats.MeanMakespan:F2}").ConfigureAwait(false);
        await writer.WriteLineAsync($"StdDev,{stats.StdDev:F2}").ConfigureAwait(false);
        await writer.WriteLineAsync($"ConvergenceGeneration,{stats.ConvergenceGeneration}")
            .ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // JSON — statistics + schedule
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asynchronously exports algorithm statistics and the full schedule to a JSON file.
    /// </summary>
    /// <param name="stats">Statistical summary.</param>
    /// <param name="schedule">The schedule to include. Pass <see langword="null"/> to omit.</param>
    /// <param name="jobs">Jobs used to resolve schedule details. Pass <see langword="null"/> to omit schedule.</param>
    /// <param name="path">Destination file path.</param>
    /// <param name="cancellationToken">Token that cancels the write.</param>
    public static async Task ExportJsonAsync(
        AlgorithmStats stats,
        Schedule? schedule,
        List<Job>? jobs,
        string path,
        CancellationToken cancellationToken = default)
    {
        var rows = (schedule is not null && jobs is not null)
            ? BuildScheduleRows(schedule, jobs)
            : null;

        var payload = new
        {
            stats = new
            {
                stats.BestMakespan,
                MeanMakespan = Math.Round(stats.MeanMakespan, 2),
                StdDev = Math.Round(stats.StdDev, 2),
                stats.ConvergenceGeneration,
            },
            schedule = rows?.Select(r => new
            {
                r.Machine,
                r.JobId,
                r.OperationId,
                r.Start,
                r.Duration,
                r.End,
            }),
        };

        string json = JsonSerializer.Serialize(
            payload,
            new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Convergence curve CSV
    // -------------------------------------------------------------------------

    /// <summary>
    /// Asynchronously exports a convergence curve — one row per generation — to a CSV file.
    /// </summary>
    /// <param name="points">Convergence data collected during a run.</param>
    /// <param name="path">Destination file path.</param>
    /// <param name="cancellationToken">Token that cancels the write.</param>
    public static async Task ExportConvergenceAsync(
        IReadOnlyList<ConvergencePoint> points,
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        await writer.WriteLineAsync("Generation,BestMakespan").ConfigureAwait(false);

        foreach (var p in points)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync($"{p.Generation},{p.BestMakespan}")
                .ConfigureAwait(false);
        }
    }

    // -------------------------------------------------------------------------
    // Plain-text Gantt table (for TUI display)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a fixed-width text Gantt table suitable for display in a terminal.
    /// Rows are sorted by machine name then start time.
    /// </summary>
    /// <param name="schedule">The schedule to render.</param>
    /// <param name="jobs">Jobs used to resolve operation details.</param>
    /// <returns>A multi-line string containing the formatted table.</returns>
    public static string BuildGanttTable(Schedule schedule, List<Job> jobs)
    {
        var rows = BuildScheduleRows(schedule, jobs);
        rows.Sort((a, b) =>
            string.Compare(a.Machine, b.Machine, StringComparison.Ordinal) != 0
                ? string.Compare(a.Machine, b.Machine, StringComparison.Ordinal)
                : a.Start.CompareTo(b.Start));

        // Compute column widths.
        int machineW = Math.Max(7, rows.Max(r => r.Machine.Length));
        const int numW = 5;

        var sb = new StringBuilder();
        string header = $"{"Machine",-7}  {"Job",numW}  {"Op",numW}  {"Start",numW}  {"Dur",numW}  {"End",numW}";
        string sep = new string('-', header.Length);

        sb.AppendLine(header);
        sb.AppendLine(sep);

        foreach (var row in rows)
            sb.AppendLine(
                $"{row.Machine,-7}  {row.JobId,numW}  {row.OperationId,numW}  " +
                $"{row.Start,numW}  {row.Duration,numW}  {row.End,numW}");

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private record ScheduleRow(
        string Machine, int JobId, int OperationId, int Start, int Duration, int End);

    private static List<ScheduleRow> BuildScheduleRows(Schedule schedule, List<Job> jobs)
    {
        var rows = new List<ScheduleRow>(jobs.Sum(j => j.Operations.Count));
        foreach (var job in jobs)
            foreach (var op in job.Operations)
            {
                int start = schedule.GetStartTime(op.JobId, op.OperationId);
                rows.Add(new ScheduleRow(
                    op.Subdivision, op.JobId, op.OperationId,
                    start, op.ProcessingTime, start + op.ProcessingTime));
            }
        return rows;
    }
}
