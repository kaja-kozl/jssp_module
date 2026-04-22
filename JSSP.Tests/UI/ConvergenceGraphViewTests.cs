using JSSP.UI.Views;

namespace JSSP.Tests.UI;

/// <summary>
/// Unit tests for the internal scaling helpers in <see cref="ConvergenceGraphView"/>.
/// These methods are pure math — no Terminal.Gui Application context is required.
/// </summary>
[TestClass]
public class ConvergenceGraphViewTests
{
    // -------------------------------------------------------------------------
    // ScaleToColumn
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ScaleToColumn_FirstGeneration_ReturnsZero()
    {
        // Arrange
        int generation = 0, maxGeneration = 100, plotWidth = 50;

        // Act
        int col = ConvergenceGraphView.ScaleToColumn(generation, maxGeneration, plotWidth);

        // Assert
        Assert.AreEqual(0, col);
    }

    [TestMethod]
    public void ScaleToColumn_LastGeneration_ReturnsRightmostColumn()
    {
        // Arrange
        int generation = 100, maxGeneration = 100, plotWidth = 50;

        // Act
        int col = ConvergenceGraphView.ScaleToColumn(generation, maxGeneration, plotWidth);

        // Assert
        Assert.AreEqual(49, col);
    }

    [TestMethod]
    public void ScaleToColumn_MaxGenerationZero_ReturnsZeroWithoutDivideByZero()
    {
        // Arrange — single generation, no progression recorded yet
        int generation = 0, maxGeneration = 0, plotWidth = 20;

        // Act
        int col = ConvergenceGraphView.ScaleToColumn(generation, maxGeneration, plotWidth);

        // Assert
        Assert.AreEqual(0, col);
    }

    [TestMethod]
    public void ScaleToColumn_PlotWidthOne_ReturnsZeroWithoutDivideByZero()
    {
        // Arrange
        int generation = 50, maxGeneration = 100, plotWidth = 1;

        // Act
        int col = ConvergenceGraphView.ScaleToColumn(generation, maxGeneration, plotWidth);

        // Assert
        Assert.AreEqual(0, col);
    }

    [TestMethod]
    public void ScaleToColumn_MidGeneration_ReturnsMidColumn()
    {
        // Arrange
        int generation = 50, maxGeneration = 100, plotWidth = 101;

        // Act
        int col = ConvergenceGraphView.ScaleToColumn(generation, maxGeneration, plotWidth);

        // Assert — 50/100 * 100 = 50
        Assert.AreEqual(50, col);
    }

    // -------------------------------------------------------------------------
    // ScaleToRow
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ScaleToRow_BestMakespan_ReturnsBottomRow()
    {
        // Arrange — lowest makespan (best result) should appear at the bottom
        int makespan = 500, min = 500, max = 1500, plotHeight = 10;

        // Act
        int row = ConvergenceGraphView.ScaleToRow(makespan, min, max, plotHeight);

        // Assert — bottom row index = plotHeight - 1
        Assert.AreEqual(9, row);
    }

    [TestMethod]
    public void ScaleToRow_WorstMakespan_ReturnsTopRow()
    {
        // Arrange — highest makespan (worst result) should appear at the top
        int makespan = 1500, min = 500, max = 1500, plotHeight = 10;

        // Act
        int row = ConvergenceGraphView.ScaleToRow(makespan, min, max, plotHeight);

        // Assert
        Assert.AreEqual(0, row);
    }

    [TestMethod]
    public void ScaleToRow_AllSameMakespan_ReturnsBottomRowWithoutDivideByZero()
    {
        // Arrange — flat convergence: min == max, guard must prevent div-by-zero
        int makespan = 1000, min = 1000, max = 1000, plotHeight = 10;

        // Act
        int row = ConvergenceGraphView.ScaleToRow(makespan, min, max, plotHeight);

        // Assert — falls back to bottom row
        Assert.AreEqual(9, row);
    }

    [TestMethod]
    public void ScaleToRow_PlotHeightOne_ReturnsZeroWithoutDivideByZero()
    {
        // Arrange
        int makespan = 800, min = 500, max = 1500, plotHeight = 1;

        // Act
        int row = ConvergenceGraphView.ScaleToRow(makespan, min, max, plotHeight);

        // Assert — single-row plot: only valid index is 0 (= plotHeight - 1)
        Assert.AreEqual(0, row);
    }

    [TestMethod]
    public void ScaleToRow_MidMakespan_ReturnsMidRow()
    {
        // Arrange
        int makespan = 1000, min = 500, max = 1500, plotHeight = 11;

        // Act
        int row = ConvergenceGraphView.ScaleToRow(makespan, min, max, plotHeight);

        // Assert — ratio = 0.5 → (1 - 0.5) * 10 = 5
        Assert.AreEqual(5, row);
    }
}
