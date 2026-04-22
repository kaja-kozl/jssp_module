using JSSP.Core.Graph;
using JSSP.Core.Models;

namespace JSSP.Tests.Core;

[TestClass]
public class CriticalPathFinderTests
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

    // ------------------------------------------------------------------ FindMakespan

    [TestMethod]
    public void FindMakespan_SingleJobSingleOp_EqualsThatOpsProcessingTime()
    {
        // Arrange: Job0 op0 M0 t=5. No disjunctive arcs.
        // Graph: S→op0(w=0)→T(w=5). Makespan = 5.
        var graph = DisjunctiveGraph.Build(new[] { MakeJob(0, ("M0", 5)) });
        var finder = new CriticalPathFinder();

        // Act
        int makespan = finder.FindMakespan(graph);

        // Assert
        Assert.AreEqual(5, makespan);
    }

    [TestMethod]
    public void FindMakespan_SingleJobTwoOps_SumOfProcessingTimes()
    {
        // Arrange: Job0 op0 M0 t=3 → op1 M1 t=2. No disjunctive arcs.
        // Graph: S→op0(w=0)→op1(w=3)→T(w=2). Makespan = 5.
        var graph = DisjunctiveGraph.Build(new[] { MakeJob(0, ("M0", 3), ("M1", 2)) });
        var finder = new CriticalPathFinder();

        // Act
        int makespan = finder.FindMakespan(graph);

        // Assert
        Assert.AreEqual(5, makespan);
    }

    [TestMethod]
    public void FindMakespan_TwoJobsSameMachineOriented_ReflectsSerialExecution()
    {
        // Arrange: Job0 op0 M0 t=3; Job1 op0 M0 t=4.
        // Schedule: J0 starts 0, J1 starts 3 → J0→J1 orientation.
        // dist[T] = dist[J1]+4 = (dist[J0]+3)+4 = 7. Makespan = 7.
        var job0 = MakeJob(0, ("M0", 3));
        var job1 = MakeJob(1, ("M0", 4));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });
        graph.Orient(MakeSchedule((0, 0, 0, 3), (1, 0, 3, 4)));
        var finder = new CriticalPathFinder();

        // Act
        int makespan = finder.FindMakespan(graph);

        // Assert
        Assert.AreEqual(7, makespan);
    }

    [TestMethod]
    public void FindMakespan_AfterReverseArc_MakespanReflectsNewOrientation()
    {
        // Arrange: same two-job M0 instance, J0→J1 (makespan 7). Reverse to J1→J0.
        // After reverse: dist[J0-op0] = 0+4=4 (from J1 via disj w=4).
        // dist[T] = max(J0→T: 4+3=7, J1→T: 0+4=4) = 7. Symmetric; makespan unchanged.
        var job0 = MakeJob(0, ("M0", 3));
        var job1 = MakeJob(1, ("M0", 4));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });
        var nodeJ0 = graph.OperationNodes[0];
        var nodeJ1 = graph.OperationNodes[1];

        graph.Orient(MakeSchedule((0, 0, 0, 3), (1, 0, 3, 4)));
        graph.ReverseArc(nodeJ0, nodeJ1);
        var finder = new CriticalPathFinder();

        // Act
        int makespan = finder.FindMakespan(graph);

        // Assert
        Assert.AreEqual(7, makespan);
    }

    [TestMethod]
    public void FindMakespan_TwoByTwoInstance_CorrectMakespan()
    {
        // Arrange: 2-job 2-machine instance.
        // Job0: op0 M0 t=3 → op1 M1 t=2
        // Job1: op0 M1 t=4 → op1 M0 t=1
        // Schedule: J0-op0=0, J1-op0=0, J0-op1=4, J1-op1=4 → Makespan=6.
        // Critical path uses the M1 disjunctive arc: S→J1-op0→J0-op1→T (0+4+2=6).
        var job0 = MakeJob(0, ("M0", 3), ("M1", 2));
        var job1 = MakeJob(1, ("M1", 4), ("M0", 1));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });

        graph.Orient(MakeSchedule(
            (0, 0, 0, 3),
            (0, 1, 4, 2),
            (1, 0, 0, 4),
            (1, 1, 4, 1)));

        var finder = new CriticalPathFinder();

        // Act
        int makespan = finder.FindMakespan(graph);

        // Assert
        Assert.AreEqual(6, makespan);
    }

    // ------------------------------------------------------------------ Find (path)

    [TestMethod]
    public void Find_SingleJobSingleOp_PathIsSourceOpSink()
    {
        // Arrange
        var graph = DisjunctiveGraph.Build(new[] { MakeJob(0, ("M0", 5)) });
        var opNode = graph.OperationNodes[0];
        var finder = new CriticalPathFinder();

        // Act
        var path = finder.Find(graph);

        // Assert
        Assert.HasCount(3, path);
        Assert.AreSame(graph.Source, path[0]);
        Assert.AreSame(opNode, path[1]);
        Assert.AreSame(graph.Sink, path[2]);
    }

    [TestMethod]
    public void Find_TwoJobsSameMachine_PathTraversesDisjunctiveArc()
    {
        // Arrange: J0→J1 orientation (J0 at 0, J1 at 3).
        // Expected path: [S, J0-op0, J1-op0, T]
        var job0 = MakeJob(0, ("M0", 3));
        var job1 = MakeJob(1, ("M0", 4));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });
        var nodeJ0 = graph.OperationNodes[0];
        var nodeJ1 = graph.OperationNodes[1];

        graph.Orient(MakeSchedule((0, 0, 0, 3), (1, 0, 3, 4)));
        var finder = new CriticalPathFinder();

        // Act
        var path = finder.Find(graph);

        // Assert
        Assert.HasCount(4, path);
        Assert.AreSame(graph.Source, path[0]);
        Assert.AreSame(nodeJ0, path[1]);
        Assert.AreSame(nodeJ1, path[2]);
        Assert.AreSame(graph.Sink, path[3]);
    }

    [TestMethod]
    public void Find_AfterReverseArc_PathOrderChanges()
    {
        // Arrange: J0→J1 then reverse to J1→J0.
        // Expected path after reverse: [S, J1-op0, J0-op0, T]
        var job0 = MakeJob(0, ("M0", 3));
        var job1 = MakeJob(1, ("M0", 4));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });
        var nodeJ0 = graph.OperationNodes[0];
        var nodeJ1 = graph.OperationNodes[1];

        graph.Orient(MakeSchedule((0, 0, 0, 3), (1, 0, 3, 4)));
        graph.ReverseArc(nodeJ0, nodeJ1);
        var finder = new CriticalPathFinder();

        // Act
        var path = finder.Find(graph);

        // Assert
        Assert.HasCount(4, path);
        Assert.AreSame(graph.Source, path[0]);
        Assert.AreSame(nodeJ1, path[1]);
        Assert.AreSame(nodeJ0, path[2]);
        Assert.AreSame(graph.Sink, path[3]);
    }

    [TestMethod]
    public void Find_TwoByTwoInstance_PathTraversesMachineConflictArc()
    {
        // Arrange: 2x2 instance (same as FindMakespan_TwoByTwoInstance).
        // Critical path: S → J1-op0 → J0-op1 → T (via M1 disjunctive arc).
        var job0 = MakeJob(0, ("M0", 3), ("M1", 2));
        var job1 = MakeJob(1, ("M1", 4), ("M0", 1));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });

        // OperationNodes: [J0-op0, J0-op1, J1-op0, J1-op1]
        var j0op1 = graph.OperationNodes[1]; // Job0 op1 on M1
        var j1op0 = graph.OperationNodes[2]; // Job1 op0 on M1

        graph.Orient(MakeSchedule(
            (0, 0, 0, 3),
            (0, 1, 4, 2),
            (1, 0, 0, 4),
            (1, 1, 4, 1)));

        var finder = new CriticalPathFinder();

        // Act
        var path = finder.Find(graph);

        // Assert: critical path S → J1-op0 → J0-op1 → T
        Assert.HasCount(4, path);
        Assert.AreSame(graph.Source, path[0]);
        Assert.AreSame(j1op0, path[1]);
        Assert.AreSame(j0op1, path[2]);
        Assert.AreSame(graph.Sink, path[3]);
    }

    [TestMethod]
    public void Find_StartsWithSourceAndEndsWithSink()
    {
        // Arrange: any oriented graph — path must always begin at Source and end at Sink.
        var job0 = MakeJob(0, ("M0", 2), ("M1", 3));
        var job1 = MakeJob(1, ("M1", 1), ("M0", 4));
        var graph = DisjunctiveGraph.Build(new[] { job0, job1 });
        graph.Orient(MakeSchedule((0, 0, 0, 2), (0, 1, 2, 3), (1, 0, 0, 1), (1, 1, 2, 4)));
        var finder = new CriticalPathFinder();

        // Act
        var path = finder.Find(graph);

        // Assert
        Assert.AreSame(graph.Source, path[0]);
        Assert.AreSame(graph.Sink, path[path.Count - 1]);
    }
}
