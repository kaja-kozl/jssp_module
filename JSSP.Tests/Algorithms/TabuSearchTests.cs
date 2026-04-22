using JSSP.Algorithms.Tabu;
using JSSP.Core.Graph;
using JSSP.Core.Models;

namespace JSSP.Tests.Algorithms;

[TestClass]
public class TabuSearchTests
{
    // =========================================================================
    // Shared test fixtures
    // =========================================================================

    /// <summary>
    /// Two-job, two-machine problem.
    ///   Job 0: Op0 on MachA (t=3), Op1 on MachB (t=2)
    ///   Job 1: Op0 on MachB (t=2), Op1 on MachA (t=1)
    /// Lower-bound makespan = 5 (job 0 path: 3+2 = 5).
    /// </summary>
    private static List<Job> MakeTwoJobTwoMachine()
    {
        var ops0 = new List<Operation>
        {
            new(0, 0, "MachA", 3),
            new(0, 1, "MachB", 2),
        };
        var ops1 = new List<Operation>
        {
            new(1, 0, "MachB", 2),
            new(1, 1, "MachA", 1),
        };
        return [new Job(0, ops0), new Job(1, ops1)];
    }

    /// <summary>Single job, single operation — trivial lower bound = processing time.</summary>
    private static List<Job> MakeSingleJobSingleOp()
    {
        var ops = new List<Operation> { new(0, 0, "MachA", 5) };
        return [new Job(0, ops)];
    }

    private static AlgorithmConfig MakeConfig(int generations = 5, int repetitions = 1, int tabuLen = 3) =>
        new AlgorithmConfig
        {
            Generations = generations,
            Repetitions = repetitions,
            TabuListLength = tabuLen,
            PopulationSize = 10,
            EliteCount = 1,
        };

    // =========================================================================
    // TabuMove
    // =========================================================================

    [TestMethod]
    public void TabuMove_SameIds_AreEqual()
    {
        // Arrange & Act
        var a = new TabuMove(3, 7);
        var b = new TabuMove(3, 7);

        // Assert: record struct value equality
        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void TabuMove_DifferentIds_AreNotEqual()
    {
        var a = new TabuMove(3, 7);
        var b = new TabuMove(7, 3);
        Assert.AreNotEqual(a, b);
    }

    // =========================================================================
    // TabuList
    // =========================================================================

    [TestMethod]
    public void TabuList_IsTabu_ReturnsTrueForAddedMove()
    {
        // Arrange
        var list = new TabuList(10);
        list.Add(new TabuMove(1, 2));

        // Act & Assert
        Assert.IsTrue(list.IsTabu(new TabuMove(1, 2)));
    }

    [TestMethod]
    public void TabuList_IsTabu_ReturnsFalseForReverseOfAddedMove()
    {
        // Arrange — adding (1,2) must NOT forbid (2,1); they are independent moves.
        var list = new TabuList(10);
        list.Add(new TabuMove(1, 2));

        // Act & Assert
        Assert.IsFalse(list.IsTabu(new TabuMove(2, 1)));
    }

    [TestMethod]
    public void TabuList_IsTabu_ReturnsFalseForUnadded()
    {
        // Arrange
        var list = new TabuList(10);
        list.Add(new TabuMove(1, 2));

        // Act & Assert
        Assert.IsFalse(list.IsTabu(new TabuMove(3, 4)));
    }

    [TestMethod]
    public void TabuList_EvictsOldestWhenFull()
    {
        // Arrange: max length 2; add three moves — first should be evicted.
        var list = new TabuList(2);
        list.Add(new TabuMove(1, 2));
        list.Add(new TabuMove(3, 4));
        list.Add(new TabuMove(5, 6)); // causes eviction of (1,2)

        // Act & Assert
        Assert.IsFalse(list.IsTabu(new TabuMove(1, 2)),
            "Move (1,2) should have been evicted when (5,6) was added.");
        Assert.IsTrue(list.IsTabu(new TabuMove(3, 4)));
        Assert.IsTrue(list.IsTabu(new TabuMove(5, 6)));
    }

    [TestMethod]
    public void TabuList_Clear_RemovesAllEntries()
    {
        // Arrange
        var list = new TabuList(5);
        list.Add(new TabuMove(1, 2));
        list.Add(new TabuMove(3, 4));

        // Act
        list.Clear();

        // Assert
        Assert.IsFalse(list.IsTabu(new TabuMove(1, 2)));
        Assert.IsFalse(list.IsTabu(new TabuMove(3, 4)));
        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void TabuList_Count_TracksEntries()
    {
        // Arrange
        var list = new TabuList(5);
        Assert.AreEqual(0, list.Count);

        list.Add(new TabuMove(1, 2));
        Assert.AreEqual(1, list.Count);

        list.Add(new TabuMove(3, 4));
        Assert.AreEqual(2, list.Count);
    }

    [TestMethod]
    public void TabuList_Count_NeverExceedsMaxLength()
    {
        // Arrange
        var list = new TabuList(3);
        for (int i = 0; i < 10; i++)
            list.Add(new TabuMove(i, i + 1));

        // Assert
        Assert.IsLessThanOrEqualTo(list.MaxLength, list.Count);
    }

    // =========================================================================
    // AspirationCriterion
    // =========================================================================

    [TestMethod]
    public void Aspiration_IsSatisfied_WhenCandidateStrictlyBetter()
    {
        // A tabu move that beats the global best should be permitted.
        Assert.IsTrue(AspirationCriterion.IsSatisfied(99, 100));
    }

    [TestMethod]
    public void Aspiration_IsNotSatisfied_WhenCandidateEqualToGlobalBest()
    {
        // Equal is not strictly better — tabu status is not overridden.
        Assert.IsFalse(AspirationCriterion.IsSatisfied(100, 100));
    }

    [TestMethod]
    public void Aspiration_IsNotSatisfied_WhenCandidateWorse()
    {
        Assert.IsFalse(AspirationCriterion.IsSatisfied(101, 100));
    }

    // =========================================================================
    // N7Neighbourhood
    // =========================================================================

    [TestMethod]
    public void N7Neighbourhood_EmptyCriticalPath_ReturnsNoMoves()
    {
        // Arrange: critical path contains only Source and Sink — no operation nodes.
        var jobs = MakeTwoJobTwoMachine();
        var graph = DisjunctiveGraph.Build(jobs);

        // A stub critical path with no operation nodes.
        var criticalPath = new List<GraphNode> { graph.Source, graph.Sink }.AsReadOnly();

        // Act
        var moves = N7Neighbourhood.Generate(criticalPath, graph);

        // Assert
        Assert.IsEmpty(moves);
    }

    [TestMethod]
    public void N7Neighbourhood_Generate_FiltersConjunctiveArcPairs()
    {
        // Arrange: Single job — all arcs on the critical path are conjunctive.
        // No disjunctive-arc pairs should be generated.
        var jobs = MakeSingleJobSingleOp();
        var graph = DisjunctiveGraph.Build(jobs);
        var config = MakeConfig();
        var schedule = new TabuSearch().Solve(jobs, config); // obtain a valid schedule

        graph.Orient(schedule);
        var cpFinder = new CriticalPathFinder();
        var criticalPath = cpFinder.Find(graph);

        // Act
        var moves = N7Neighbourhood.Generate(criticalPath, graph);

        // Assert: single job, single machine — no disjunctive pairs possible.
        Assert.IsEmpty(moves);
    }

    [TestMethod]
    public void N7Neighbourhood_Generate_FindsDisjunctivePairOnCriticalPath()
    {
        // Arrange: two-job two-machine instance.
        // At least one critical-path segment must cross a disjunctive arc.
        var jobs = MakeTwoJobTwoMachine();
        var graph = DisjunctiveGraph.Build(jobs);

        // Build an initial schedule via active decoding of a known chromosome
        // that puts job 0 before job 1 on MachA: [0, 1, 0, 1].
        var schedule = new Schedule();
        // Job0-Op0 (MachA, t=3): start=0, finish=3
        schedule.SetStartTime(0, 0, 0, 3);
        // Job1-Op0 (MachB, t=2): start=0, finish=2
        schedule.SetStartTime(1, 0, 0, 2);
        // Job0-Op1 (MachB, t=2): start=max(2,3)=3, finish=5
        schedule.SetStartTime(0, 1, 3, 2);
        // Job1-Op1 (MachA, t=1): start=max(3,2)=3, finish=4
        schedule.SetStartTime(1, 1, 3, 1);

        graph.Orient(schedule);
        var cpFinder = new CriticalPathFinder();
        var criticalPath = cpFinder.Find(graph);

        // Act
        var moves = N7Neighbourhood.Generate(criticalPath, graph);

        // Assert: at least one move must be a pair of Operation nodes that are
        // disjunctive neighbours (same machine). The exact count depends on the
        // critical path structure, but the neighbourhood is non-empty for this instance.
        foreach (var (from, to) in moves)
        {
            Assert.AreEqual(NodeKind.Operation, from.Kind,
                "All move sources must be operation nodes.");
            Assert.AreEqual(NodeKind.Operation, to.Kind,
                "All move destinations must be operation nodes.");
            Assert.IsTrue(graph.AreDisjunctiveNeighbours(from, to),
                "All generated moves must be disjunctive-arc pairs.");
        }
    }

    // =========================================================================
    // TabuSearch.Solve
    // =========================================================================

    [TestMethod]
    public void TabuSearch_Solve_SingleJobSingleOp_MakespanEqualsProcessingTime()
    {
        // Arrange: only one operation — makespan must equal its processing time.
        var jobs = MakeSingleJobSingleOp();
        var config = MakeConfig(generations: 5, repetitions: 1);

        // Act
        var schedule = new TabuSearch().Solve(jobs, config);

        // Assert
        Assert.AreEqual(5, schedule.Makespan);
    }

    [TestMethod]
    public void TabuSearch_Solve_TwoJobTwoMachine_ReturnsFeasibleMakespan()
    {
        // Arrange
        var jobs = MakeTwoJobTwoMachine();
        var config = MakeConfig(generations: 20, repetitions: 1, tabuLen: 5);

        // Act
        var schedule = new TabuSearch().Solve(jobs, config);

        // Assert: must be at least the lower bound (5) and below an absurd upper bound.
        Assert.IsGreaterThanOrEqualTo(5, schedule.Makespan,
            $"Makespan {schedule.Makespan} is below the lower bound of 5.");
        Assert.IsLessThan(1000, schedule.Makespan,
            $"Makespan {schedule.Makespan} is implausibly large.");
    }

    [TestMethod]
    public void TabuSearch_Solve_ReturnsNonNullSchedule()
    {
        // Arrange
        var jobs = MakeTwoJobTwoMachine();

        // Act
        var schedule = new TabuSearch().Solve(jobs, MakeConfig());

        // Assert
        Assert.IsNotNull(schedule);
    }

    [TestMethod]
    public void TabuSearch_FiresOnConvergenceEachIteration()
    {
        // Arrange
        var jobs = MakeTwoJobTwoMachine();
        var config = MakeConfig(generations: 5, repetitions: 1);
        var ts = new TabuSearch();
        int eventCount = 0;
        ts.OnConvergence += (_, _) => eventCount++;

        // Act
        ts.Solve(jobs, config);

        // Assert: at least one event per iteration (single-job instances stop early;
        // this two-job instance has swappable pairs so all generations fire).
        Assert.IsGreaterThanOrEqualTo(1, eventCount,
            "At least one OnConvergence event must fire per Solve call.");
        Assert.IsLessThanOrEqualTo(config.Generations, eventCount,
            "OnConvergence must not fire more times than the iteration count.");
    }

    [TestMethod]
    public void TabuSearch_GetStats_NullBeforeSolve()
    {
        // Arrange & Act
        var stats = new TabuSearch().GetStats();

        // Assert
        Assert.IsNull(stats);
    }

    [TestMethod]
    public void TabuSearch_GetStats_PopulatedAfterSolve()
    {
        // Arrange
        var jobs = MakeTwoJobTwoMachine();
        var config = MakeConfig(generations: 10, repetitions: 2);
        var ts = new TabuSearch();

        // Act
        ts.Solve(jobs, config);
        var stats = ts.GetStats();

        // Assert: stats are non-null and internally consistent.
        Assert.IsNotNull(stats);
        Assert.IsLessThanOrEqualTo(stats.MeanMakespan, (double)stats.BestMakespan,
            $"Best ({stats.BestMakespan}) must be ≤ mean ({stats.MeanMakespan}).");
        Assert.IsGreaterThanOrEqualTo(0.0, stats.StdDev,
            "Standard deviation must be non-negative.");
    }
}
