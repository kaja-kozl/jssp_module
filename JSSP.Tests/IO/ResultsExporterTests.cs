using JSSP.Core.Models;
using JSSP.IO;

namespace JSSP.Tests.IO;

[TestClass]
public sealed class ResultsExporterTests
{
    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    private static (AlgorithmStats stats, Schedule schedule, List<Job> jobs) BuildMinimalFixture()
    {
        var op = new Operation(1, 1, "M1", 10);
        var job = new Job(1, new[] { op }.AsReadOnly());
        var jobs = new List<Job> { job };

        var schedule = new Schedule();
        schedule.SetStartTime(1, 1, 0, 10);

        var stats = new AlgorithmStats
        {
            BestMakespan = 10,
            MeanMakespan = 10.0,
            StdDev = 0.0,
            ConvergenceGeneration = 1,
        };

        return (stats, schedule, jobs);
    }

    private static string TempPath(string suffix) =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"jssp_test_{Guid.NewGuid():N}{suffix}");

    // -------------------------------------------------------------------------
    // ExportCsvAsync
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ExportCsvAsync_CreatesFile()
    {
        // Arrange
        var (stats, schedule, jobs) = BuildMinimalFixture();
        string path = TempPath(".csv");

        try
        {
            // Act
            await ResultsExporter.ExportCsvAsync(stats, schedule, jobs, path);

            // Assert
            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ExportCsvAsync_ContainsHeaderRow()
    {
        // Arrange
        var (stats, schedule, jobs) = BuildMinimalFixture();
        string path = TempPath(".csv");

        try
        {
            // Act
            await ResultsExporter.ExportCsvAsync(stats, schedule, jobs, path);
            string firstLine = (await File.ReadAllLinesAsync(path))[0];

            // Assert
            Assert.Contains("Machine", firstLine);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ExportCsvAsync_ContainsDataRow()
    {
        // Arrange
        var (stats, schedule, jobs) = BuildMinimalFixture();
        string path = TempPath(".csv");

        try
        {
            // Act
            await ResultsExporter.ExportCsvAsync(stats, schedule, jobs, path);
            string[] lines = await File.ReadAllLinesAsync(path);

            // Assert: at least header + one data row.
            Assert.IsGreaterThanOrEqualTo(2, lines.Length);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // -------------------------------------------------------------------------
    // ExportJsonAsync
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ExportJsonAsync_CreatesFile()
    {
        // Arrange
        var (stats, schedule, jobs) = BuildMinimalFixture();
        string path = TempPath(".json");

        try
        {
            // Act
            await ResultsExporter.ExportJsonAsync(stats, schedule, jobs, path);

            // Assert
            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ExportJsonAsync_ContainsBestMakespanKey()
    {
        // Arrange
        var (stats, schedule, jobs) = BuildMinimalFixture();
        string path = TempPath(".json");

        try
        {
            // Act
            await ResultsExporter.ExportJsonAsync(stats, schedule, jobs, path);
            string content = await File.ReadAllTextAsync(path);

            // Assert
            Assert.Contains("BestMakespan", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // -------------------------------------------------------------------------
    // ExportConvergenceAsync
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ExportConvergenceAsync_CreatesFile()
    {
        // Arrange
        var points = new List<ConvergencePoint>
        {
            new(1, 200),
            new(2, 180),
            new(3, 160),
        };
        string path = TempPath(".csv");

        try
        {
            // Act
            await ResultsExporter.ExportConvergenceAsync(points, path);

            // Assert
            Assert.IsTrue(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ExportConvergenceAsync_CorrectLineCount()
    {
        // Arrange: 3 data points → header + 3 rows = 4 non-empty lines.
        var points = new List<ConvergencePoint>
        {
            new(1, 200),
            new(2, 180),
            new(3, 160),
        };
        string path = TempPath(".csv");

        try
        {
            // Act
            await ResultsExporter.ExportConvergenceAsync(points, path);
            string[] lines = (await File.ReadAllLinesAsync(path))
                .Where(l => l.Length > 0)
                .ToArray();

            // Assert
            Assert.HasCount(4, lines);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ExportConvergenceAsync_HeaderIsCorrect()
    {
        // Arrange
        var points = new List<ConvergencePoint> { new(1, 100) };
        string path = TempPath(".csv");

        try
        {
            // Act
            await ResultsExporter.ExportConvergenceAsync(points, path);
            string header = (await File.ReadAllLinesAsync(path))[0];

            // Assert
            Assert.AreEqual("Generation,BestMakespan", header);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // -------------------------------------------------------------------------
    // BuildGanttTable
    // -------------------------------------------------------------------------

    [TestMethod]
    public void BuildGanttTable_ContainsHeaderLine()
    {
        // Arrange
        var (_, schedule, jobs) = BuildMinimalFixture();

        // Act
        string table = ResultsExporter.BuildGanttTable(schedule, jobs);

        // Assert
        Assert.Contains("Machine", table);
    }

    [TestMethod]
    public void BuildGanttTable_ContainsMachineData()
    {
        // Arrange
        var (_, schedule, jobs) = BuildMinimalFixture();

        // Act
        string table = ResultsExporter.BuildGanttTable(schedule, jobs);

        // Assert: M1 must appear as the machine for our single operation.
        Assert.Contains("M1", table);
    }
}
