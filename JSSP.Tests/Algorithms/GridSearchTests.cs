using JSSP.Algorithms.Genetic;
using JSSP.Algorithms.HyperParameter;
using JSSP.Core.Models;

namespace JSSP.Tests.Algorithms;

[TestClass]
public sealed class GridSearchTests
{
    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    private static List<Job> BuildTwoJobInstance()
    {
        var job1 = new Job(1,
        [
            new Operation(1, 1, "M1", 5),
            new Operation(1, 2, "M2", 3),
        ]);
        var job2 = new Job(2,
        [
            new Operation(2, 1, "M2", 4),
            new Operation(2, 2, "M1", 2),
        ]);
        return [job1, job2];
    }

    // -------------------------------------------------------------------------
    // ParameterGrid.Enumerate
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ParameterGrid_SmallOverrides_EnumeratesCorrectCount()
    {
        // Arrange: 2 pop × 2 mut × 1 tour × 1 tabu × 1 inj = 4 configs.
        int[] pops = [50, 100];
        double[] muts = [0.01, 0.02];
        int[] tours = [3];
        int[] tabus = [5];
        int[] injs = [5];

        // Act
        var configs = ParameterGrid.Enumerate(pops, muts, tours, tabus, injs).ToList();

        // Assert
        Assert.HasCount(4, configs);
    }

    [TestMethod]
    public void ParameterGrid_SmallOverrides_ContainsExpectedCombinations()
    {
        // Arrange
        int[] pops = [50, 100];
        double[] muts = [0.01, 0.02];
        int[] tours = [3];
        int[] tabus = [5];
        int[] injs = [5];

        // Act
        var configs = ParameterGrid.Enumerate(pops, muts, tours, tabus, injs).ToList();

        // Assert: all four Cartesian pairs present.
        Assert.IsTrue(configs.Any(c => c.PopulationSize == 50 && c.MutationRate == 0.01));
        Assert.IsTrue(configs.Any(c => c.PopulationSize == 50 && c.MutationRate == 0.02));
        Assert.IsTrue(configs.Any(c => c.PopulationSize == 100 && c.MutationRate == 0.01));
        Assert.IsTrue(configs.Any(c => c.PopulationSize == 100 && c.MutationRate == 0.02));
    }

    [TestMethod]
    public void ParameterGrid_DefaultAxes_AllCombinationsUnique()
    {
        // Arrange + Act
        var configs = ParameterGrid.Enumerate().ToList();
        var keys = configs
            .Select(c => (c.PopulationSize, c.MutationRate, c.TournamentSize, c.TabuListLength, c.InjectionFrequencyK))
            .ToHashSet();

        // Assert: no duplicate configs (compare plain ints to avoid MSTEST0037).
        int configCount = configs.Count;
        int keyCount = keys.Count;
        Assert.AreEqual(configCount, keyCount);
    }

    [TestMethod]
    public void ParameterGrid_DefaultAxes_TotalCountMatchesCartesianProduct()
    {
        // Arrange: 7 × 10 × 5 × 4 × 5 = 7000.
        const int expected = 7 * 10 * 5 * 4 * 5;

        // Act
        int count = ParameterGrid.Enumerate().Count();

        // Assert: plain int comparison — not a collection assertion.
        Assert.AreEqual(expected, count);
    }

    // -------------------------------------------------------------------------
    // ParameterGrid.Sample
    // -------------------------------------------------------------------------

    [TestMethod]
    public void ParameterGrid_Sample_ReturnsRequestedCount()
    {
        // Arrange + Act
        var samples = ParameterGrid.Sample(10, new Random(42));

        // Assert
        Assert.HasCount(10, samples);
    }

    [TestMethod]
    public void ParameterGrid_Sample_NoDuplicates()
    {
        // Arrange + Act
        var samples = ParameterGrid.Sample(50, new Random(0));
        var keys = samples
            .Select(c => (c.PopulationSize, c.MutationRate, c.TournamentSize, c.TabuListLength, c.InjectionFrequencyK))
            .ToHashSet();

        // Assert: no duplicates.
        int sampleCount = samples.Count;
        int keyCount = keys.Count;
        Assert.AreEqual(sampleCount, keyCount);
    }

    [TestMethod]
    public void ParameterGrid_Sample_CountExceedsGrid_ReturnsFullGrid()
    {
        // Arrange: single-axis grid of 2 points; request 100.
        int[] pops = [50, 100];
        double[] muts = [0.01];
        int[] tours = [3];
        int[] tabus = [5];
        int[] injs = [5];

        // Act
        var samples = ParameterGrid.Sample(100, new Random(0), pops, muts, tours, tabus, injs);

        // Assert: capped at grid size (2).
        Assert.HasCount(2, samples);
    }

    // -------------------------------------------------------------------------
    // RandomSearch.Run
    // -------------------------------------------------------------------------

    [TestMethod]
    public void RandomSearch_SmallSample_ReturnsValidResult()
    {
        // Arrange
        var jobs = BuildTwoJobInstance();

        // Act
        var result = RandomSearch.Run(
            () => new GeneticAlgorithm(),
            jobs,
            sampleCount: 2,
            rng: new Random(1),
            populationSizes: [10, 20],
            mutationRates: [0.01],
            tournamentSizes: [3],
            tabuListLengths: [5],
            injectionFrequencies: [5],
            generations: 5,
            repetitions: 1);

        // Assert
        Assert.IsNotNull(result.BestConfig);
        Assert.IsGreaterThan(0, result.BestStats.BestMakespan);
        Assert.AreEqual(2, result.ConfigsEvaluated);
    }
}
