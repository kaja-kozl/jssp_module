using JSSP.Core.Models;
using JSSP.IO;

namespace JSSP.Tests.IO;

/// <summary>Tests for <see cref="GanttChartExporter"/>.</summary>
[TestClass]
public class GanttChartExporterTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal 2-job 2-machine schedule with known start times.
    /// Job 0: M1(0..5) → M2(5..8)
    /// Job 1: M2(0..4) → M1(5..7)
    /// Makespan = 8.
    /// </summary>
    private static (List<Job> Jobs, Schedule Schedule) BuildMinimalSchedule()
    {
        var ops0 = new List<Operation>
        {
            new(0, 0, "M1", 5),
            new(0, 1, "M2", 3),
        };
        var ops1 = new List<Operation>
        {
            new(1, 0, "M2", 4),
            new(1, 1, "M1", 2),
        };

        var jobs = new List<Job>
        {
            new(0, ops0),
            new(1, ops1),
        };

        var schedule = new Schedule();
        schedule.SetStartTime(0, 0, 0, 5);
        schedule.SetStartTime(0, 1, 5, 3);
        schedule.SetStartTime(1, 0, 0, 4);
        schedule.SetStartTime(1, 1, 5, 2);

        return (jobs, schedule);
    }

    private static string TempXlsxPath() =>
        Path.Combine(Path.GetTempPath(), $"jssp_test_gantt_{Guid.NewGuid()}.xlsx");

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    /// <summary>Happy path: valid schedule produces a non-empty .xlsx file.</summary>
    [TestMethod]
    public void Export_CreatesNonEmptyFile_ForValidSchedule()
    {
        // Arrange
        var (jobs, schedule) = BuildMinimalSchedule();
        string path = TempXlsxPath();

        try
        {
            // Act
            GanttChartExporter.Export(jobs, schedule, path);

            // Assert
            Assert.IsTrue(File.Exists(path));
            Assert.IsGreaterThanOrEqualTo(1L, new FileInfo(path).Length);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>Single job, single operation — edge case with makespan = processing time.</summary>
    [TestMethod]
    public void Export_DoesNotThrow_ForSingleJobSingleOperation()
    {
        // Arrange
        var ops = new List<Operation> { new(0, 0, "M1", 5) };
        var jobs = new List<Job> { new(0, ops) };
        var schedule = new Schedule();
        schedule.SetStartTime(0, 0, 0, 5);
        string path = TempXlsxPath();

        try
        {
            // Act
            GanttChartExporter.Export(jobs, schedule, path);

            // Assert
            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>Empty job list — Gantt sheet is skipped (makespan = 0), data sheet is empty.</summary>
    [TestMethod]
    public void Export_DoesNotThrow_ForEmptyJobList()
    {
        // Arrange
        var jobs = new List<Job>();
        var schedule = new Schedule();
        string path = TempXlsxPath();

        try
        {
            // Act
            GanttChartExporter.Export(jobs, schedule, path);

            // Assert — workbook still written
            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
