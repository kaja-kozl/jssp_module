using JSSP.Algorithms.Memetic;
using JSSP.Core.Interfaces;
using JSSP.Core.Models;

namespace JSSP.Tests.Algorithms;

[TestClass]
public sealed class MemeticHybridTests
{
    // -------------------------------------------------------------------------
    // Test helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// 2-job, 2-op instance.
    /// Job 1: M1(5) → M2(3)
    /// Job 2: M2(4) → M1(2)
    /// Minimum makespan = 9 (J1 then J2 on M1; J2 then J1 on M2).
    /// </summary>
    private static List<Job> BuildTwoJobInstance() =>
    [
        new Job(1,
        [
            new Operation(1, 1, "M1", 5),
            new Operation(1, 2, "M2", 3),
        ]),
        new Job(2,
        [
            new Operation(2, 1, "M2", 4),
            new Operation(2, 2, "M1", 2),
        ]),
    ];

    private static AlgorithmConfig BuildSmallConfig(int repetitions = 1, int generations = 5) =>
        new()
        {
            PopulationSize = 10,
            Generations = generations,
            Repetitions = repetitions,
            MutationRate = 0.02,
            TournamentSize = 3,
            EliteCount = 1,
            TabuListLength = 3,
            InjectionFrequencyK = 2,
        };

    // -------------------------------------------------------------------------
    // Solve — basic correctness
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Solve_ReturnsFeasibleSchedule()
    {
        // Arrange
        var hybrid = new MemeticHybrid();
        var jobs = BuildTwoJobInstance();

        // Act
        Schedule result = hybrid.Solve(jobs, BuildSmallConfig());

        // Assert: non-zero makespan (schedule is not empty).
        Assert.IsGreaterThan(0, result.Makespan);
    }

    [TestMethod]
    public void Solve_MakespanAtLeastLowerBound()
    {
        // Arrange: lower bound = max total processing time per machine.
        // M1: job1-op1(5) + job2-op2(2) = 7. M2: job1-op2(3) + job2-op1(4) = 7.
        // So best possible makespan >= 7.
        var hybrid = new MemeticHybrid();
        var jobs = BuildTwoJobInstance();

        // Act
        Schedule result = hybrid.Solve(jobs, BuildSmallConfig());

        // Assert
        Assert.IsGreaterThanOrEqualTo(7, result.Makespan);
    }

    [TestMethod]
    public void Solve_PopulatesAlgorithmStats()
    {
        // Arrange
        var hybrid = new MemeticHybrid();
        var jobs = BuildTwoJobInstance();

        // Act
        hybrid.Solve(jobs, BuildSmallConfig());
        var stats = hybrid.GetStats();

        // Assert
        Assert.IsNotNull(stats);
        Assert.IsGreaterThan(0, stats.BestMakespan);
    }

    // -------------------------------------------------------------------------
    // Convergence counter resets between repetitions
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Solve_ConvergenceGenerationResetsPerRepetition()
    {
        // Arrange: 2 reps × 5 gens. Expect generation numbers 1–5 to appear at least
        // twice (once per rep), proving the counter resets and does not accumulate.
        var hybrid = new MemeticHybrid();
        var jobs = BuildTwoJobInstance();
        var config = BuildSmallConfig(repetitions: 2, generations: 5);

        var recordedGenerations = new List<int>();
        hybrid.OnConvergence += (_, args) => recordedGenerations.Add(args.Generation);

        // Act
        hybrid.Solve(jobs, config);

        // Assert: generation 1 must appear at least twice (once per repetition).
        int countOfGen1 = recordedGenerations.Count(g => g == 1);
        Assert.IsGreaterThanOrEqualTo(2, countOfGen1);
    }

    [TestMethod]
    public void Solve_TotalConvergenceEventsMatchGenerations()
    {
        // Arrange: 2 reps × 5 gens = 10 events expected (one per gen per rep).
        var hybrid = new MemeticHybrid();
        var jobs = BuildTwoJobInstance();
        var config = BuildSmallConfig(repetitions: 2, generations: 5);

        int eventCount = 0;
        hybrid.OnConvergence += (_, _) => eventCount++;

        // Act
        hybrid.Solve(jobs, config);

        // Assert
        Assert.AreEqual(10, eventCount);
    }

    // -------------------------------------------------------------------------
    // Cancellation
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Solve_CancelledImmediately_ReturnsFallbackSchedule()
    {
        // Arrange
        var hybrid = new MemeticHybrid();
        var jobs = BuildTwoJobInstance();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var config = new AlgorithmConfig
        {
            PopulationSize = 10,
            Generations = 50,
            Repetitions = 3,
            CancellationToken = cts.Token,
            InjectionFrequencyK = 5,
        };

        // Act: should not throw.
        Schedule result = hybrid.Solve(jobs, config);

        // Assert: fallback produces a valid (non-zero) makespan.
        Assert.IsGreaterThan(0, result.Makespan);
    }

    // -------------------------------------------------------------------------
    // Stats
    // -------------------------------------------------------------------------

    [TestMethod]
    public void GetStats_BeforeSolve_ReturnsNull()
    {
        // Arrange
        var hybrid = new MemeticHybrid();

        // Act + Assert
        Assert.IsNull(hybrid.GetStats());
    }

    [TestMethod]
    public void GetStats_AfterMultipleReps_StdDevIsNonNegative()
    {
        // Arrange
        var hybrid = new MemeticHybrid();
        var jobs = BuildTwoJobInstance();

        // Act
        hybrid.Solve(jobs, BuildSmallConfig(repetitions: 3, generations: 3));
        var stats = hybrid.GetStats();

        // Assert
        Assert.IsNotNull(stats);
        Assert.IsGreaterThanOrEqualTo(0.0, (double)stats.StdDev);
    }
}
