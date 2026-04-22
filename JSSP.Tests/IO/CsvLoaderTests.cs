using JSSP.IO;

namespace JSSP.Tests.IO;

[TestClass]
public class CsvLoaderTests
{
    // -------------------------------------------------------------------------
    // LoadAsync tests
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task LoadAsync_ReturnsCorrectJobCount()
    {
        // Arrange
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n" +
                     "1,1,Machining,10\n" +
                     "1,2,Assembly,5\n" +
                     "2,1,Welding,8\n" +
                     "2,2,Painting,3\n";
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, csv);

        try
        {
            // Act
            CsvLoadResult result = await CsvLoader.LoadAsync(path);

            // Assert
            Assert.HasCount(2, result.Jobs);
            Assert.HasCount(2, result.Jobs[0].Operations);
            Assert.HasCount(2, result.Jobs[1].Operations);
            Assert.IsEmpty(result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadAsync_ThrowsOnMissingFile()
    {
        // Arrange
        string path = "does_not_exist_async.csv";

        // Act & Assert
        try
        {
            await CsvLoader.LoadAsync(path);
            Assert.Fail("Expected FileNotFoundException was not thrown.");
        }
        catch (FileNotFoundException)
        {
            // expected — test passes
        }
    }

    [TestMethod]
    public async Task LoadAsync_ReturnsEmptyJobsForHeaderOnly()
    {
        // Arrange
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n";
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, csv);

        try
        {
            // Act
            CsvLoadResult result = await CsvLoader.LoadAsync(path);

            // Assert
            Assert.IsEmpty(result.Jobs);
            Assert.IsEmpty(result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadAsync_SkipsMalformedRowsAndWarns()
    {
        // Arrange — one good row, one row with non-integer ProcessingTime
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n" +
                     "1,1,Machining,10\n" +
                     "2,1,Assembly,NOTANUMBER\n";
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, csv);

        try
        {
            // Act
            CsvLoadResult result = await CsvLoader.LoadAsync(path);

            // Assert
            Assert.HasCount(1, result.Jobs);
            Assert.HasCount(1, result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadAsync_SkipsNegativeProcessingTimeAndWarns()
    {
        // Arrange
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n" +
                     "1,1,Machining,-5\n";
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, csv);

        try
        {
            // Act
            CsvLoadResult result = await CsvLoader.LoadAsync(path);

            // Assert
            Assert.IsEmpty(result.Jobs);
            Assert.HasCount(1, result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadAsync_SkipsZeroProcessingTimeAndWarns()
    {
        // Arrange
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n" +
                     "1,1,Machining,0\n";
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, csv);

        try
        {
            // Act
            CsvLoadResult result = await CsvLoader.LoadAsync(path);

            // Assert
            Assert.IsEmpty(result.Jobs);
            Assert.HasCount(1, result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadAsync_SkipsBlankSubdivisionAndWarns()
    {
        // Arrange
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n" +
                     "1,1,   ,10\n";
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, csv);

        try
        {
            // Act
            CsvLoadResult result = await CsvLoader.LoadAsync(path);

            // Assert
            Assert.IsEmpty(result.Jobs);
            Assert.HasCount(1, result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadAsync_SkipsDuplicateOperationAndWarns()
    {
        // Arrange — two rows with identical (JobId=1, OperationId=1)
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n" +
                     "1,1,Machining,10\n" +
                     "1,1,Assembly,5\n";
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, csv);

        try
        {
            // Act
            CsvLoadResult result = await CsvLoader.LoadAsync(path);

            // Assert — first occurrence kept, second rejected
            Assert.HasCount(1, result.Jobs);
            Assert.HasCount(1, result.Jobs[0].Operations);
            Assert.HasCount(1, result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadAsync_HandlesBomEncodedFile()
    {
        // Arrange — prepend UTF-8 BOM to the CSV bytes
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n" +
                     "1,1,Machining,10\n";
        byte[] bom = [0xEF, 0xBB, 0xBF];
        byte[] csvBytes = System.Text.Encoding.UTF8.GetBytes(csv);
        byte[] fileBytes = [.. bom, .. csvBytes];

        string path = Path.GetTempFileName();
        await File.WriteAllBytesAsync(path, fileBytes);

        try
        {
            // Act
            CsvLoadResult result = await CsvLoader.LoadAsync(path);

            // Assert — BOM must not corrupt the header or first data row
            Assert.HasCount(1, result.Jobs);
            Assert.IsEmpty(result.Warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadAsync_RespectsCancellationToken()
    {
        // Arrange — token cancelled before the call
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n" +
                     "1,1,Machining,10\n";
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, csv);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            // Act & Assert
            try
            {
                await CsvLoader.LoadAsync(path, cts.Token);
                Assert.Fail("Expected OperationCanceledException was not thrown.");
            }
            catch (OperationCanceledException)
            {
                // expected — test passes
            }
        }
        finally
        {
            File.Delete(path);
        }
    }


    [TestMethod]
    public void Load_ReturnsCorrectJobCount()
    {
        // Arrange
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n" +
                     "1,1,Machining,10\n" +
                     "1,2,Assembly,5\n" +
                     "2,1,Welding,8\n" +
                     "2,2,Painting,3\n";
        string path = Path.GetTempFileName();
        File.WriteAllText(path, csv);

        try
        {
            // Act
            var jobs = CsvLoader.Load(path);

            // Assert
            Assert.HasCount(2, jobs);
            Assert.HasCount(2, jobs[0].Operations);
            Assert.HasCount(2, jobs[1].Operations);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_ThrowsOnMissingFile()
    {
        // Arrange
        string path = "does_not_exist.csv";

        // Act & Assert
        try
        {
            CsvLoader.Load(path);
            Assert.Fail("Expected FileNotFoundException was not thrown.");
        }
        catch (FileNotFoundException)
        {
            // expected — test passes
        }
    }

    [TestMethod]
    public void Load_ReturnsEmptyListForHeaderOnlyFile()
    {
        // Arrange
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n";
        string path = Path.GetTempFileName();
        File.WriteAllText(path, csv);

        try
        {
            // Act
            var jobs = CsvLoader.Load(path);

            // Assert
            Assert.IsEmpty(jobs);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Load_SkipsMalformedRows()
    {
        // Arrange
        string csv = "JobId,OperationId,Subdivision,ProcessingTime\n" +
                     "1,1,Machining,10\n" +
                     "bad,row,data\n" +
                     "1,2,Assembly,5\n";
        string path = Path.GetTempFileName();
        File.WriteAllText(path, csv);

        try
        {
            // Act
            var jobs = CsvLoader.Load(path);

            // Assert
            Assert.HasCount(1, jobs);
            Assert.HasCount(2, jobs[0].Operations);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
