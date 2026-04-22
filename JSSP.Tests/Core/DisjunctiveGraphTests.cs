using JSSP.Core.Graph;
using JSSP.Core.Models;

namespace JSSP.Tests.Core;

[TestClass]
public class DisjunctiveGraphTests
{
    // ------------------------------------------------------------------ helpers

    private static Job MakeJob(int jobId, params (string Machine, int Time)[] ops)
    {
        var operations = ops
            .Select((o, i) => new Operation(jobId, i, o.Machine, o.Time))
            .ToList();
        return new Job(jobId, operations);
    }

    private static Schedule MakeSchedule(
        params (int JobId, int OpId, int Start, int Duration)[] assignments)
    {
        var schedule = new Schedule();
        foreach (var (jobId, opId, start, duration) in assignments)
            schedule.SetStartTime(jobId, opId, start, duration);
        return schedule;
    }

    // ------------------------------------------------------------------ Build tests

    [TestMethod]
    public void Build_SingleJobSingleOp_ConjunctiveEdgesSourceToOpToSink()
    {
        // Arrange
        var job = MakeJob(0, ("M0", 5));

        // Act
        var graph = DisjunctiveGraph.Build(new[] { job });
        var opNode = graph.OperationNodes[0];

        // Assert — structure
        Assert.HasCount(1, graph.OperationNodes);

        // Source → opNode (weight 0, conjunctive)
        var sourceSucc = graph.GetSuccessors(graph.Source);
        Assert.HasCount(1, sourceSucc);
        Assert.AreSame(opNode, sourceSucc[0].To);
        Assert.AreEqual(0, sourceSucc[0].Weight);
        Assert.AreEqual(EdgeType.Conjunctive, sourceSucc[0].Type);

        // opNode → Sink (weight = processing time, conjunctive)
        var opSucc = graph.GetSuccessors(opNode);
        Assert.HasCount(1, opSucc);
        Assert.AreSame(graph.Sink, opSucc[0].To);
        Assert.AreEqual(5, opSucc[0].Weight);
        Assert.AreEqual(EdgeType.Conjunctive, opSucc[0].Type);
    }

    [TestMethod]
    public void Build_SingleJobMultipleOps_ConjunctiveChainIsCorrect()
    {
        // Arrange: Job 0 — op0 M0 t=2 → op1 M1 t=3 → op2 M2 t=4
        var job = MakeJob(0, ("M0", 2), ("M1", 3), ("M2", 4));

        // Act
        var graph = DisjunctiveGraph.Build(new[] { job });

        // Assert — 3 ops, chain Source→op0→op1→op2→Sink
        Assert.HasCount(3, graph.OperationNodes);

        var op0 = graph.OperationNodes[0];
        var op1 = graph.OperationNodes[1];
        var op2 = graph.OperationNodes[2];

        // Source → op0 weight 0
        var srcSucc = graph.GetSuccessors(graph.Source);
        Assert.HasCount(1, srcSucc);
        Assert.AreSame(op0, srcSucc[0].To);
        Assert.AreEqual(0, srcSucc[0].Weight);

        // op0 → op1 weight 2
        var op0Succ = graph.GetSuccessors(op0);
        Assert.HasCount(1, op0Succ);
        Assert.AreSame(op1, op0Succ[0].To);
        Assert.AreEqual(2, op0Succ[0].Weight);

        // op1 → op2 weight 3
        var op1Succ = graph.GetSuccessors(op1);
        Assert.HasCount(1, op1Succ);
        Assert.AreSame(op2, op1Succ[0].To);
        Assert.AreEqual(3, op1Succ[0].Weight);

        // op2 → Sink weight 4
        var op2Succ = graph.GetSuccessors(op2);
        Assert.HasCount(1, op2Succ);
        Assert.AreSame(graph.Sink, op2Succ[0].To);
        Assert.AreEqual(4, op2Succ[0].Weight);
    }

    [TestMethod]
    public void Build_TwoJobsSameMachine_DisjunctivePairExists()
    {
        // Arrange: both jobs have their first op on M0
        var job0 = MakeJob(0, ("M0", 3));
        var job1 = MakeJob(1, ("M0", 4));

        // Act
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });

        var nodeJ0 = graph.OperationNodes[0];
        var nodeJ1 = graph.OperationNodes[1];

        // Assert
        Assert.IsTrue(graph.AreDisjunctiveNeighbours(nodeJ0, nodeJ1));
    }

    [TestMethod]
    public void Build_TwoJobsDifferentMachines_NoDisjunctivePair()
    {
        // Arrange
        var job0 = MakeJob(0, ("M0", 3));
        var job1 = MakeJob(1, ("M1", 4));

        // Act
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });

        var nodeJ0 = graph.OperationNodes[0];
        var nodeJ1 = graph.OperationNodes[1];

        // Assert
        Assert.IsFalse(graph.AreDisjunctiveNeighbours(nodeJ0, nodeJ1));
    }

    [TestMethod]
    public void Build_EmptyJobList_OnlySourceAndSink()
    {
        // Act
        var graph = DisjunctiveGraph.Build(Array.Empty<Job>());

        // Assert
        Assert.IsEmpty(graph.OperationNodes);
        Assert.IsEmpty(graph.GetSuccessors(graph.Source));
        Assert.IsEmpty(graph.GetSuccessors(graph.Sink));
    }

    // ------------------------------------------------------------------ GetMachinePeers tests

    [TestMethod]
    public void GetMachinePeers_ReturnsOnlyOpsOnSameMachine()
    {
        // Arrange: Job0 M0, Job1 M0, Job2 M1
        var job0 = MakeJob(0, ("M0", 2));
        var job1 = MakeJob(1, ("M0", 3));
        var job2 = MakeJob(2, ("M1", 1));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1, job2 });

        var nodeJ0 = graph.OperationNodes[0]; // M0
        var nodeJ1 = graph.OperationNodes[1]; // M0
        var nodeJ2 = graph.OperationNodes[2]; // M1

        // Act
        var peers = graph.GetMachinePeers(nodeJ0);

        // Assert: only J1 is a peer of J0 (same machine); J2 is on a different machine
        Assert.HasCount(1, peers);
        Assert.AreSame(nodeJ1, peers[0]);
        Assert.IsFalse(peers.Contains(nodeJ2));
    }

    [TestMethod]
    public void GetMachinePeers_SourceNode_ReturnsEmpty()
    {
        var job = MakeJob(0, ("M0", 5));
        var graph = DisjunctiveGraph.Build(new[] { job });

        Assert.IsEmpty(graph.GetMachinePeers(graph.Source));
    }

    // ------------------------------------------------------------------ Orient tests

    [TestMethod]
    public void Orient_DirectionMatchesScheduleStartOrder()
    {
        // Arrange
        // Job 0: op0 M0 t=3 → op1 M1 t=2
        // Job 1: op0 M1 t=4 → op1 M0 t=1
        // Schedule: J0-op0=0, J1-op0=0, J0-op1=4, J1-op1=4
        // M0 pair (J0-op0 vs J1-op1): J0-op0 at 0 < J1-op1 at 4 → J0-op0 → J1-op1
        // M1 pair (J0-op1 vs J1-op0): J1-op0 at 0 < J0-op1 at 4 → J1-op0 → J0-op1
        var job0 = MakeJob(0, ("M0", 3), ("M1", 2));
        var job1 = MakeJob(1, ("M1", 4), ("M0", 1));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });

        var schedule = MakeSchedule(
            (0, 0, 0, 3),   // J0-op0 on M0: start 0
            (0, 1, 4, 2),   // J0-op1 on M1: start 4
            (1, 0, 0, 4),   // J1-op0 on M1: start 0
            (1, 1, 4, 1));  // J1-op1 on M0: start 4

        // OperationNodes order matches job/op enumeration order
        var j0op0 = graph.OperationNodes[0]; // Job0 op0 M0
        var j0op1 = graph.OperationNodes[1]; // Job0 op1 M1
        var j1op0 = graph.OperationNodes[2]; // Job1 op0 M1
        var j1op1 = graph.OperationNodes[3]; // Job1 op1 M0

        // Act
        graph.Orient(schedule);

        // Assert M0 pair: J0-op0 → J1-op1
        Assert.IsTrue(graph.GetSuccessors(j0op0).Any(e => e.To == j1op1 && e.Type == EdgeType.Disjunctive));
        Assert.IsFalse(graph.GetSuccessors(j1op1).Any(e => e.To == j0op0 && e.Type == EdgeType.Disjunctive));

        // Assert M1 pair: J1-op0 → J0-op1
        Assert.IsTrue(graph.GetSuccessors(j1op0).Any(e => e.To == j0op1 && e.Type == EdgeType.Disjunctive));
        Assert.IsFalse(graph.GetSuccessors(j0op1).Any(e => e.To == j1op0 && e.Type == EdgeType.Disjunctive));
    }

    [TestMethod]
    public void Orient_ReplacesExistingOrientation()
    {
        // Arrange: two single-op jobs on M0
        var job0 = MakeJob(0, ("M0", 3));
        var job1 = MakeJob(1, ("M0", 4));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });

        var nodeJ0 = graph.OperationNodes[0];
        var nodeJ1 = graph.OperationNodes[1];

        // First orientation: J0 starts before J1 → J0 → J1
        graph.Orient(MakeSchedule((0, 0, 0, 3), (1, 0, 3, 4)));
        Assert.IsTrue(graph.GetSuccessors(nodeJ0).Any(e => e.To == nodeJ1));

        // Second orientation: J1 starts before J0 → J1 → J0
        graph.Orient(MakeSchedule((0, 0, 4, 3), (1, 0, 0, 4)));

        // Assert: arc J0→J1 gone, arc J1→J0 present
        Assert.IsFalse(graph.GetSuccessors(nodeJ0).Any(e => e.To == nodeJ1));
        Assert.IsTrue(graph.GetSuccessors(nodeJ1).Any(e => e.To == nodeJ0));
    }

    // ------------------------------------------------------------------ ReverseArc tests

    [TestMethod]
    public void ReverseArc_FlipsArcDirection()
    {
        // Arrange: J0 → J1 after Orient
        var job0 = MakeJob(0, ("M0", 3));
        var job1 = MakeJob(1, ("M0", 4));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });

        var nodeJ0 = graph.OperationNodes[0];
        var nodeJ1 = graph.OperationNodes[1];

        graph.Orient(MakeSchedule((0, 0, 0, 3), (1, 0, 3, 4)));

        // Act
        graph.ReverseArc(nodeJ0, nodeJ1);

        // Assert: J0→J1 gone, J1→J0 present with weight = J1.ProcessingTime
        Assert.IsFalse(graph.GetSuccessors(nodeJ0).Any(e => e.To == nodeJ1 && e.Type == EdgeType.Disjunctive));
        var reversedArc = graph.GetSuccessors(nodeJ1)
            .FirstOrDefault(e => e.To == nodeJ0 && e.Type == EdgeType.Disjunctive);
        Assert.IsNotNull(reversedArc);
        Assert.AreEqual(4, reversedArc.Weight); // weight = J1.ProcessingTime
    }

    [TestMethod]
    public void ReverseArc_ApplyThenUndo_RestoresOriginalDirection()
    {
        // Arrange
        var job0 = MakeJob(0, ("M0", 3));
        var job1 = MakeJob(1, ("M0", 4));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });

        var nodeJ0 = graph.OperationNodes[0];
        var nodeJ1 = graph.OperationNodes[1];

        graph.Orient(MakeSchedule((0, 0, 0, 3), (1, 0, 3, 4)));

        // Act — apply then undo
        graph.ReverseArc(nodeJ0, nodeJ1);
        graph.ReverseArc(nodeJ1, nodeJ0);

        // Assert: back to J0→J1
        Assert.IsTrue(graph.GetSuccessors(nodeJ0).Any(e => e.To == nodeJ1 && e.Type == EdgeType.Disjunctive));
        Assert.IsFalse(graph.GetSuccessors(nodeJ1).Any(e => e.To == nodeJ0 && e.Type == EdgeType.Disjunctive));
    }

    [TestMethod]
    public void ReverseArc_NonExistentArc_ThrowsArgumentException()
    {
        // Arrange: J0 → J1 but we attempt to reverse J1 → J0 (which does not exist)
        var job0 = MakeJob(0, ("M0", 3));
        var job1 = MakeJob(1, ("M0", 4));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });

        var nodeJ0 = graph.OperationNodes[0];
        var nodeJ1 = graph.OperationNodes[1];

        graph.Orient(MakeSchedule((0, 0, 0, 3), (1, 0, 3, 4)));

        // Act & Assert
        Assert.ThrowsExactly<ArgumentException>(() => graph.ReverseArc(nodeJ1, nodeJ0));
    }
}
