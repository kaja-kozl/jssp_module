namespace JSSP.IO;

/// <summary>
/// The result of a <see cref="CsvLoader.LoadAsync"/> call.
/// Carries the successfully parsed jobs together with any per-row warnings
/// generated during parsing (e.g. invalid values, duplicate operations).
/// </summary>
/// <param name="Jobs">
/// Ordered list of jobs built from valid rows in the file.
/// Never null; may be empty if every data row was rejected.
/// </param>
/// <param name="Warnings">
/// Human-readable warning messages, one per rejected row.
/// Each message includes the 1-based line number and reason for rejection.
/// Empty when the file contained no invalid rows.
/// </param>
public sealed record CsvLoadResult(
    IReadOnlyList<JSSP.Core.Models.Job> Jobs,
    IReadOnlyList<string> Warnings);
