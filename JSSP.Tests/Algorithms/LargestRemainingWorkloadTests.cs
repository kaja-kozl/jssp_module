using JSSP.Algorithms.Constructive;
using JSSP.Core.Models;

namespace JSSP.Tests.Algorithms;

[TestClass]
public class LargestRemainingWorkloadTests
{
    // -------------------------------------------------------------------------
    // Test fixture builders
    // -------------------------------------------------------------------------

    /// <summary>
    /// Two jobs with different total workloads.
    ///   Job 1: ops [MachineA:3, MachineB:5]  → workload = 8
    ///   Job 2: ops [MachineA:20, MachineB:5] → workload = 25
    /// Job 2 has the higher workload, so it should be scheduled first.
    /// </summary>
    private static List<Job> MakeTwoJobsDifferentWorkload()
    {
        var ops1 = new List<Operation>
        {
            new(1, 0, "MachineA", 3),
            new(1, 1, "MachineB", 5),
        };
        var ops2 = new List<Operation>
        {
            new(2, 0, "MachineA", 20),
            new(2, 1, "MachineB", 5),
        };
        return [new Job(1, ops1), new Job(2, ops2)];
    }

    /// <summary>
    /// Two jobs with identical total workloads — exercises random tie-breaking.
    ///   Job 1: ops [MachineA:10]  → workload = 10
    ///   Job 2: ops [MachineB:10]  → workload = 10
    /// </summary>
    private static List<Job> MakeTwoJobsSameWorkload()
    {
        var ops1 = new List<Operation> { new(1, 0, "MachineA", 10) };
        var ops2 = new List<Operation> { new(2, 0, "MachineB", 10) };
        return [new Job(1, ops1), new Job(2, ops2)];
    }

    /// <summary>Single job with one operation.</summary>
    private static List<Job> MakeSingleJobSingleOp()
    {
        var ops = new List<Operation> { new(0, 0, "MachineA", 7) };
        return [new Job(0, ops)];
    }

    /// <summary>
    /// Three jobs with two operations each — larger problem for gene-pool integrity check.
    ///   Job 0: [A:5, B:3]  → workload = 8
    ///   Job 1: [A:12, B:2] → workload = 14
    ///   Job 2: [B:6, A:1]  → workload = 7
    /// Expected first gene: Job 1 (highest workload = 14).
    /// </summary>
    private static List<Job> MakeThreeJobsTwoOpsEach()
    {
        var ops0 = new List<Operation> { new(0, 0, "MachineA", 5), new(0, 1, "MachineB", 3) };
        var ops1 = new List<Operation> { new(1, 0, "MachineA", 12), new(1, 1, "MachineB", 2) };
        var ops2 = new List<Operation> { new(2, 0, "MachineB", 6), new(2, 1, "MachineA", 1) };
        return [new Job(0, ops0), new Job(1, ops1), new Job(2, ops2)];
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Create_ReturnsArrayWithCorrectLength()
    {
        // Arrange: 2 jobs with 2 ops each → total 4 genes.
        var jobs = MakeTwoJobsDifferentWorkload();
        var rng = new Random(0);

        // Act
        int[] genes = LargestRemainingWorkload.Create(jobs, rng);

        // Assert
        Assert.HasCount(4, genes);
    }

    [TestMethod]
    public void Create_GenePoolMatchesJobOperationCounts()
    {
        // Arrange: Job 1 has 2 ops, Job 2 has 2 ops.
        var jobs = MakeTwoJobsDifferentWorkload();
        var rng = new Random(0);

        // Act
        int[] genes = LargestRemainingWorkload.Create(jobs, rng);

        // Assert: each job ID appears exactly as many times as it has operations.
        Assert.HasCount(2, genes.Where(g => g == 1), "Job 1 should appear twice.");
        Assert.HasCount(2, genes.Where(g => g == 2), "Job 2 should appear twice.");
    }

    [TestMethod]
    public void Create_FirstGeneIsHighestWorkloadJob()
    {
        // Arrange: Job 2 has workload 25, Job 1 has workload 8 — no tie at step 0.
        var jobs = MakeTwoJobsDifferentWorkload();
        var rng = new Random(0);

        // Act
        int[] genes = LargestRemainingWorkload.Create(jobs, rng);

        // Assert: first gene must be Job 2 (deterministic — no tie at step 0).
        Assert.AreEqual(2, genes[0], "The job with the highest workload should be scheduled first.");
    }

    [TestMethod]
    public void Create_SingleJobSingleOperation_ReturnsOneGene()
    {
        // Arrange
        var jobs = MakeSingleJobSingleOp();
        var rng = new Random(0);

        // Act
        int[] genes = LargestRemainingWorkload.Create(jobs, rng);

        // Assert
        Assert.HasCount(1, genes);
        Assert.AreEqual(0, genes[0]);
    }

    [TestMethod]
    public void Create_TiedWorkloads_StillProducesValidGenePool()
    {
        // Arrange: both jobs have workload = 10 → tie at every step.
        var jobs = MakeTwoJobsSameWorkload();
        var rng = new Random(0);

        // Act
        int[] genes = LargestRemainingWorkload.Create(jobs, rng);

        // Assert: chromosome must be valid regardless of which tie-break was chosen.
        Assert.HasCount(2, genes);
        Assert.HasCount(1, genes.Where(g => g == 1), "Job 1 should appear exactly once.");
        Assert.HasCount(1, genes.Where(g => g == 2), "Job 2 should appear exactly once.");
    }

    [TestMethod]
    public void Create_LargerProblem_AllGenesAccountedFor()
    {
        // Arrange: 3 jobs × 2 ops each → 6 total genes.
        var jobs = MakeThreeJobsTwoOpsEach();
        var rng = new Random(42);

        // Act
        int[] genes = LargestRemainingWorkload.Create(jobs, rng);

        // Assert: length correct and each job ID appears exactly twice.
        Assert.HasCount(6, genes);
        Assert.HasCount(2, genes.Where(g => g == 0), "Job 0 should appear exactly twice.");
        Assert.HasCount(2, genes.Where(g => g == 1), "Job 1 should appear exactly twice.");
        Assert.HasCount(2, genes.Where(g => g == 2), "Job 2 should appear exactly twice.");
    }

    [TestMethod]
    public void Create_LargerProblem_HighestWorkloadJobIsFirst()
    {
        // Arrange: Job 1 has workload 14 — highest of the three, no tie at step 0.
        var jobs = MakeThreeJobsTwoOpsEach();
        var rng = new Random(0);

        // Act
        int[] genes = LargestRemainingWorkload.Create(jobs, rng);

        // Assert
        Assert.AreEqual(1, genes[0], "Job 1 (workload=14) should be scheduled first.");
    }
}
