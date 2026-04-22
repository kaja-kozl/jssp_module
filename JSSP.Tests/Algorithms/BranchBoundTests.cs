using JSSP.Algorithms;
using JSSP.Core.Models;

namespace JSSP.Tests.Algorithms;

[TestClass]
public class BranchBoundTests
{
    // -------------------------------------------------------------------------
    // Fixture builders — all data hardcoded; no CSV dependency
    // -------------------------------------------------------------------------

    /// <summary>
    /// Single job, two operations on separate machines (t=3, t=5).
    /// Optimal makespan = 8 (operations are sequential, no machine contention).
    /// </summary>
    private static List<Job> MakeSingleJobTwoOps()
    {
        var ops = new List<Operation>
        {
            new(0, 0, "A", 3),
            new(0, 1, "B", 5),
        };
        return [new Job(0, ops)];
    }

    /// <summary>
    /// Two jobs on entirely different machines — they run in parallel.
    ///   Job 0: Op0 on "A" (t=4)
    ///   Job 1: Op0 on "B" (t=3)
    /// Optimal makespan = 4 (max of the two parallel durations).
    /// </summary>
    private static List<Job> MakeTwoJobsNoConflict()
    {
        return
        [
            new Job(0, new List<Operation> { new(0, 0, "A", 4) }),
            new Job(1, new List<Operation> { new(1, 0, "B", 3) }),
        ];
    }

    /// <summary>
    /// Two jobs sharing two machines — scheduling order matters.
    ///   Job 0: Op0 on "A" (t=2) → Op1 on "B" (t=3)
    ///   Job 1: Op0 on "B" (t=4) → Op1 on "A" (t=1)
    ///
    /// Manual optimal trace (schedule J1-op0 then J0-op0 on their respective machines):
    ///   J0-op0 on A: start=0, finish=2
    ///   J1-op0 on B: start=0, finish=4
    ///   J1-op1 on A: start=max(2,4)=4, finish=5
    ///   J0-op1 on B: start=max(2,4)=4, finish=7
    ///   Makespan = 7
    /// </summary>
    private static List<Job> MakeTwoJobsTwoMachineConflict()
    {
        var ops0 = new List<Operation>
        {
            new(0, 0, "A", 2),
            new(0, 1, "B", 3),
        };
        var ops1 = new List<Operation>
        {
            new(1, 0, "B", 4),
            new(1, 1, "A", 1),
        };
        return [new Job(0, ops0), new Job(1, ops1)];
    }

    /// <summary>Single job, single operation (trivial leaf-node case).</summary>
    private static List<Job> MakeSingleJobSingleOp()
    {
        return [new Job(0, new List<Operation> { new(0, 0, "X", 7) })];
    }

    private static AlgorithmConfig DefaultConfig() => new()
    {
        Repetitions = 1,
        Generations = 1,
    };

    // -------------------------------------------------------------------------
    // Solve — correctness tests
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Solve_SingleJobSingleOp_MakespanEqualsProcessingTime()
    {
        // Arrange
        var jobs = MakeSingleJobSingleOp();

        // Act
        var schedule = new BranchBound().Solve(jobs, DefaultConfig());

        // Assert: only one operation — makespan must equal processing time exactly.
        Assert.AreEqual(7, schedule.Makespan);
    }

    [TestMethod]
    public void Solve_SingleJobTwoOps_MakespanEqualsSumOfProcessingTimes()
    {
        // Arrange: no machine contention — operations are strictly sequential.
        var jobs = MakeSingleJobTwoOps();

        // Act
        var schedule = new BranchBound().Solve(jobs, DefaultConfig());

        // Assert: 3 + 5 = 8.
        Assert.AreEqual(8, schedule.Makespan);
    }

    [TestMethod]
    public void Solve_TwoJobsNoMachineConflict_MakespanIsMaxOfIndividualDurations()
    {
        // Arrange: jobs run in parallel on distinct machines.
        var jobs = MakeTwoJobsNoConflict();

        // Act
        var schedule = new BranchBound().Solve(jobs, DefaultConfig());

        // Assert: parallel execution → makespan = max(4, 3) = 4.
        Assert.AreEqual(4, schedule.Makespan);
    }

    [TestMethod]
    public void Solve_TwoJobsMachineConflict_FindsOptimalMakespan()
    {
        // Arrange: classic 2×2 instance where scheduling order determines quality.
        var jobs = MakeTwoJobsTwoMachineConflict();

        // Act
        var schedule = new BranchBound().Solve(jobs, DefaultConfig());

        // Assert: B&B must find the proven optimal of 7 (see fixture XML doc for trace).
        Assert.AreEqual(7, schedule.Makespan);
    }

    [TestMethod]
    public void Solve_ReturnsNonNullSchedule()
    {
        // Arrange
        var jobs = MakeSingleJobSingleOp();

        // Act
        var schedule = new BranchBound().Solve(jobs, DefaultConfig());

        // Assert: IAlgorithm contract — Solve must never return null.
        Assert.IsNotNull(schedule);
    }

    // -------------------------------------------------------------------------
    // Solve — cancellation
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Solve_CancelledToken_ReturnsNonNullSchedule()
    {
        // Arrange: token already cancelled before Solve is called.
        var jobs = MakeTwoJobsTwoMachineConflict();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var config = new AlgorithmConfig { CancellationToken = cts.Token };

        // Act
        var schedule = new BranchBound().Solve(jobs, config);

        // Assert: must return a non-null Schedule even when cancelled immediately.
        Assert.IsNotNull(schedule);
    }

    // -------------------------------------------------------------------------
    // GetStats
    // -------------------------------------------------------------------------

    [TestMethod]
    public void GetStats_BeforeSolve_ReturnsNull()
    {
        // Arrange & Act
        var stats = new BranchBound().GetStats();

        // Assert
        Assert.IsNull(stats);
    }

    [TestMethod]
    public void GetStats_AfterSolve_IsNotNull()
    {
        // Arrange
        var jobs = MakeSingleJobSingleOp();
        var bb = new BranchBound();

        // Act
        bb.Solve(jobs, DefaultConfig());
        var stats = bb.GetStats();

        // Assert
        Assert.IsNotNull(stats);
    }

    [TestMethod]
    public void GetStats_AfterSolve_BestMakespanMatchesSchedule()
    {
        // Arrange
        var jobs = MakeSingleJobSingleOp();
        var bb = new BranchBound();

        // Act
        var schedule = bb.Solve(jobs, DefaultConfig());
        var stats = bb.GetStats()!;

        // Assert: stats must reflect the returned schedule's makespan.
        Assert.AreEqual(schedule.Makespan, stats.BestMakespan);
    }

    [TestMethod]
    public void GetStats_AfterSolve_DeterministicAlgorithmHasZeroStdDev()
    {
        // Arrange
        var jobs = MakeTwoJobsTwoMachineConflict();
        var bb = new BranchBound();

        // Act
        bb.Solve(jobs, DefaultConfig());
        var stats = bb.GetStats()!;

        // Assert: B&B is deterministic — standard deviation must be 0.
        Assert.AreEqual(0.0, stats.StdDev);
    }

    // -------------------------------------------------------------------------
    // IsViable — tractability guard
    // -------------------------------------------------------------------------

    [TestMethod]
    public void IsViable_SmallInstance_ReturnsTrue()
    {
        // Arrange: 2 jobs × 3 ops = 6 total operations (well within limit)
        var jobs = new List<Job>
        {
            new(0, new List<Operation> { new(0,0,"A",1), new(0,1,"B",1), new(0,2,"C",1) }),
            new(1, new List<Operation> { new(1,0,"A",1), new(1,1,"B",1), new(1,2,"C",1) }),
        };

        // Act & Assert
        Assert.IsTrue(BranchBound.IsViable(jobs));
    }

    [TestMethod]
    public void IsViable_ExactlyAtLimit_ReturnsTrue()
    {
        // Arrange: 4 jobs × 5 ops = 20 total operations (boundary — still viable)
        var jobs = Enumerable.Range(0, 4)
            .Select(j => new Job(j, Enumerable.Range(0, 5)
                .Select(o => new Operation(j, o, $"M{o}", 1))
                .ToList()))
            .ToList();

        // Act & Assert
        Assert.IsTrue(BranchBound.IsViable(jobs));
    }

    [TestMethod]
    public void IsViable_OneOverLimit_ReturnsFalse()
    {
        // Arrange: 3 jobs × 7 ops = 21 total operations (one over the limit)
        var jobs = Enumerable.Range(0, 3)
            .Select(j => new Job(j, Enumerable.Range(0, 7)
                .Select(o => new Operation(j, o, $"M{o}", 1))
                .ToList()))
            .ToList();

        // Act & Assert
        Assert.IsFalse(BranchBound.IsViable(jobs));
    }

    [TestMethod]
    public void IsViable_EmptyJobList_ReturnsTrue()
    {
        // Arrange: 0 operations is trivially within the limit
        var jobs = new List<Job>();

        // Act & Assert
        Assert.IsTrue(BranchBound.IsViable(jobs));
    }

    [TestMethod]
    public void Solve_OversizedInstance_ThrowsInvalidOperationException()
    {
        // Arrange: 3 jobs × 7 ops = 21 total operations
        var jobs = Enumerable.Range(0, 3)
            .Select(j => new Job(j, Enumerable.Range(0, 7)
                .Select(o => new Operation(j, o, $"M{o}", 1))
                .ToList()))
            .ToList();

        // Act
        InvalidOperationException? caught = null;
        try { new BranchBound().Solve(jobs, new AlgorithmConfig()); }
        catch (InvalidOperationException ex) { caught = ex; }

        // Assert: must throw before any search begins; message contains the actual count
        Assert.IsNotNull(caught, "Expected InvalidOperationException but no exception was thrown.");
        Assert.Contains("21", caught.Message,
            $"Expected '21' in exception message, got: {caught.Message}");
    }

    // -------------------------------------------------------------------------
    // OnConvergence
    // -------------------------------------------------------------------------

    [TestMethod]
    public void OnConvergence_FiredAtLeastOnce_WhenSolutionFound()
    {
        // Arrange
        var jobs = MakeTwoJobsTwoMachineConflict();
        var bb = new BranchBound();
        int eventCount = 0;
        bb.OnConvergence += (_, _) => eventCount++;

        // Act
        bb.Solve(jobs, DefaultConfig());

        // Assert: at least the initial solution must have fired the event.
        Assert.IsGreaterThanOrEqualTo(1, eventCount);
    }

    [TestMethod]
    public void OnConvergence_MakespanIsMonotonicallyDecreasing()
    {
        // Arrange: each event must carry a makespan ≤ the previous event's makespan
        // because B&B only fires when a new best is found.
        var jobs = MakeTwoJobsTwoMachineConflict();
        var bb = new BranchBound();
        var makespans = new List<int>();
        bb.OnConvergence += (_, e) => makespans.Add(e.BestMakespan);

        // Act
        bb.Solve(jobs, DefaultConfig());

        // Assert
        for (int i = 1; i < makespans.Count; i++)
            Assert.IsLessThanOrEqualTo(makespans[i - 1], makespans[i],
                $"Makespan at event {i} ({makespans[i]}) should be ≤ previous ({makespans[i - 1]}).");
    }
}
