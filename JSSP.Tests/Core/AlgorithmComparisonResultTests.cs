using JSSP.Core.Models;

namespace JSSP.Tests.Core;

[TestClass]
public class AlgorithmComparisonResultTests
{
    [TestMethod]
    public void SuccessCase_HasError_IsFalse_AndStatsAreSet()
    {
        // Arrange
        var stats = new AlgorithmStats
        {
            BestMakespan = 100,
            MeanMakespan = 110.5,
            StdDev = 5.2,
            ElapsedTime = TimeSpan.FromSeconds(3),
        };

        // Act
        var result = new AlgorithmComparisonResult("GeneticAlgorithm", stats, null);

        // Assert
        Assert.AreEqual("GeneticAlgorithm", result.AlgorithmName);
        Assert.AreSame(stats, result.Stats);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsFalse(result.HasError);
    }

    [TestMethod]
    public void ErrorCase_HasError_IsTrue_AndStatsAreNull()
    {
        // Arrange / Act
        var result = new AlgorithmComparisonResult("TabuSearch", null, "Not implemented");

        // Assert
        Assert.AreEqual("TabuSearch", result.AlgorithmName);
        Assert.IsNull(result.Stats);
        Assert.AreEqual("Not implemented", result.ErrorMessage);
        Assert.IsTrue(result.HasError);
    }

    [TestMethod]
    public void TimedOutCase_HasError_IsTrue_StatsAreNull()
    {
        // Arrange / Act — timed-out rows record a warning string as ErrorMessage
        var result = new AlgorithmComparisonResult("MemeticHybrid", null, "(timed out)");

        // Assert
        Assert.IsTrue(result.HasError);
        Assert.AreEqual("(timed out)", result.ErrorMessage);
    }

    [TestMethod]
    public void SuccessWithWarning_HasError_IsTrue_WhenWarningProvided()
    {
        // Arrange — a run that completed but hit the timeout still passes a warning
        var stats = new AlgorithmStats { BestMakespan = 200 };

        // Act
        var result = new AlgorithmComparisonResult("GeneticAlgorithm", stats, "(timed out)");

        // Assert
        Assert.IsTrue(result.HasError);
        Assert.IsNotNull(result.Stats);
    }
}
