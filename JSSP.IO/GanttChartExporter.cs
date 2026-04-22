using ClosedXML.Excel;
using JSSP.Core.Models;

namespace JSSP.IO;

/// <summary>
/// Exports a computed JSSP schedule as an Excel (.xlsx) workbook containing
/// a styled tabular data sheet and a colour-coded visual Gantt chart sheet.
/// Each Gantt cell represents exactly one makespan time unit; operations are
/// merged cells labelled "J{jobId}O{operationId}" with contrast-aware text.
/// </summary>
public static class GanttChartExporter
{
    // -------------------------------------------------------------------------
    // Private types and constants
    // -------------------------------------------------------------------------

    private record ScheduleEntry(int JobId, int OperationId, string Machine, int Start, int Duration)
    {
        public int End => Start + Duration;
    }

    /// <summary>Fill colour paired with a pre-computed contrast text colour.</summary>
    private record JobColour(XLColor Fill, XLColor Text);

    private static readonly XLColor DarkText = XLColor.FromHtml("#1F1F1F");
    private static readonly XLColor HeaderBackground = XLColor.FromHtml("#1F3864");
    private static readonly XLColor MachineLabelBackground = XLColor.FromHtml("#DCE6F1");
    private static readonly XLColor IdleCellBackground = XLColor.FromHtml("#F5F5F5");
    private static readonly XLColor AlternateRowBackground = XLColor.FromHtml("#EBF3FB");
    private static readonly XLColor GridLineColour = XLColor.FromHtml("#BFBFBF");

    // Pastel fills — all light enough for dark text throughout.
    private static readonly JobColour[] JobPalette =
    [
        new(XLColor.FromHtml("#9DC3E6"), DarkText),   // pastel blue
        new(XLColor.FromHtml("#F4B183"), DarkText),   // pastel orange
        new(XLColor.FromHtml("#A9D18E"), DarkText),   // pastel green
        new(XLColor.FromHtml("#FF9999"), DarkText),   // pastel pink
        new(XLColor.FromHtml("#FFE699"), DarkText),   // pastel yellow
        new(XLColor.FromHtml("#C5A3D5"), DarkText),   // pastel purple
        new(XLColor.FromHtml("#B5EAD7"), DarkText),   // pastel mint
        new(XLColor.FromHtml("#BDD7EE"), DarkText),   // pastel sky blue
        new(XLColor.FromHtml("#FFB3A7"), DarkText),   // pastel salmon
        new(XLColor.FromHtml("#FFCBA4"), DarkText),   // pastel peach
        new(XLColor.FromHtml("#B8E4F9"), DarkText),   // pastel cyan
        new(XLColor.FromHtml("#D5F5B8"), DarkText),   // pastel lime
    ];

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Exports the given schedule to an Excel (.xlsx) file at
    /// <paramref name="outputPath"/>. Sheet 1 is a styled tabular schedule;
    /// sheet 2 is a Gantt chart with one column per makespan time unit,
    /// one row per machine, each operation as a merged colour-coded cell
    /// labelled "J{jobId}O{operationId}".
    /// </summary>
    /// <param name="jobs">Jobs from the loaded problem instance.</param>
    /// <param name="schedule">Schedule produced by the algorithm.</param>
    /// <param name="outputPath">Destination .xlsx file path.</param>
    public static void Export(IReadOnlyList<Job> jobs, Schedule schedule, string outputPath)
    {
        var entries = BuildEntries(jobs, schedule);

        using var workbook = new XLWorkbook();
        WriteDataSheet(workbook.Worksheets.Add("Schedule Data"), entries);
        WriteGanttSheet(workbook.Worksheets.Add("Gantt Chart"), entries, schedule.Makespan);
        workbook.SaveAs(outputPath);
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private static List<ScheduleEntry> BuildEntries(IReadOnlyList<Job> jobs, Schedule schedule)
    {
        var entries = new List<ScheduleEntry>();
        foreach (var job in jobs)
        {
            foreach (var op in job.Operations)
            {
                int start = schedule.GetStartTime(op.JobId, op.OperationId);
                entries.Add(new ScheduleEntry(
                    op.JobId, op.OperationId, op.Subdivision, start, op.ProcessingTime));
            }
        }
        return [.. entries.OrderBy(e => e.Machine).ThenBy(e => e.Start)];
    }

    private static void WriteDataSheet(IXLWorksheet sheet, List<ScheduleEntry> entries)
    {
        string[] headers = ["Job", "Operation", "Machine", "Start", "Duration", "End"];

        for (int c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).Value = headers[c];

        var headerRange = sheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Fill.BackgroundColor = HeaderBackground;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Row(1).Height = 20;

        int row = 2;
        foreach (var e in entries)
        {
            sheet.Cell(row, 1).Value = e.JobId;
            sheet.Cell(row, 2).Value = e.OperationId;
            sheet.Cell(row, 3).Value = e.Machine;
            sheet.Cell(row, 4).Value = e.Start;
            sheet.Cell(row, 5).Value = e.Duration;
            sheet.Cell(row, 6).Value = e.End;

            if (row % 2 == 0)
                sheet.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = AlternateRowBackground;

            row++;
        }

        if (entries.Count > 0)
        {
            var dataArea = sheet.Range(1, 1, entries.Count + 1, headers.Length);
            dataArea.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            dataArea.Style.Border.OutsideBorderColor = DarkText;
            dataArea.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            dataArea.Style.Border.InsideBorderColor = GridLineColour;
        }

        sheet.Columns(1, headers.Length).AdjustToContents();
    }

    private static void WriteGanttSheet(IXLWorksheet sheet, List<ScheduleEntry> entries, int makespan)
    {
        if (makespan <= 0)
            return;

        var machines = entries.Select(e => e.Machine).Distinct().OrderBy(m => m).ToList();
        int rowCount = machines.Count;

        var jobPaletteIndex = new Dictionary<int, int>();
        int ci = 0;
        foreach (int jobId in entries.Select(e => e.JobId).Distinct().OrderBy(id => id))
            jobPaletteIndex[jobId] = ci++ % JobPalette.Length;

        // Base coat: idle-cell tint over the entire data grid.
        sheet.Range(1, 1, rowCount + 1, makespan + 1).Style.Fill.BackgroundColor = IdleCellBackground;

        // Header row — navy background, white text.
        var headerRange = sheet.Range(1, 1, 1, makespan + 1);
        headerRange.Style.Fill.BackgroundColor = HeaderBackground;
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        sheet.Cell(1, 1).Value = "Machine \\ Time";
        sheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

        for (int t = 0; t < makespan; t++)
        {
            if (t % 10 == 0)
            {
                sheet.Cell(1, t + 2).Value = t;
                sheet.Cell(1, t + 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }

        // Machine label column — light blue-grey, dark bold text.
        if (rowCount > 0)
        {
            var labelRange = sheet.Range(2, 1, rowCount + 1, 1);
            labelRange.Style.Fill.BackgroundColor = MachineLabelBackground;
            labelRange.Style.Font.Bold = true;
            labelRange.Style.Font.FontColor = DarkText;
            labelRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            labelRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }

        // Medium border around the entire grid.
        sheet.Range(1, 1, rowCount + 1, makespan + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        sheet.Range(1, 1, rowCount + 1, makespan + 1).Style.Border.OutsideBorderColor = DarkText;

        // Machine rows — write machine label then merged operation cells.
        for (int m = 0; m < machines.Count; m++)
        {
            int row = m + 2;
            sheet.Cell(row, 1).Value = machines[m];

            var machineOps = entries
                .Where(e => e.Machine == machines[m])
                .OrderBy(e => e.Start);

            foreach (var entry in machineOps)
            {
                // Column 2 = time unit 0; time unit t → column t+2.
                // entry.End is exclusive, so last occupied column = (entry.End - 1) + 2 = entry.End + 1.
                int startCol = entry.Start + 2;
                int endCol = entry.End + 1;

                var range = sheet.Range(row, startCol, row, endCol);
                range.Merge();

                var colour = JobPalette[jobPaletteIndex[entry.JobId]];
                range.Style.Fill.BackgroundColor = colour.Fill;
                range.Style.Font.FontColor = colour.Text;
                range.Style.Font.Bold = true;
                range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                range.Style.Border.OutsideBorderColor = XLColor.White;

                sheet.Cell(row, startCol).Value = $"J{entry.JobId}O{entry.OperationId}";
            }
        }

        // Column widths: machine label wider, time columns narrow and uniform.
        sheet.Column(1).Width = 20;
        sheet.Columns(2, makespan + 1).Width = 3;

        // Row heights.
        sheet.Row(1).Height = 20;
        if (rowCount > 0)
            sheet.Rows(2, rowCount + 1).Height = 22;

        // Freeze header row and machine label column.
        sheet.SheetView.FreezeRows(1);
        sheet.SheetView.FreezeColumns(1);
    }
}
