using JSSP.Algorithms.Genetic;
using JSSP.Core.Models;

namespace JSSP.Tests.Algorithms;

[TestClass]
public class GeneticAlgorithmTests
{
    // -------------------------------------------------------------------------
    // Test fixture builders
    // -------------------------------------------------------------------------

    /// <summary>
    /// Two-job, two-machine problem used in several tests.
    ///   Job 0: Op0 on MachineA (t=2), Op1 on MachineB (t=3)
    ///   Job 1: Op0 on MachineA (t=4), Op1 on MachineB (t=1)
    /// For chromosome [0,1,0,1] the active decoder produces makespan = 7.
    /// </summary>
    private static List<Job> MakeTwoJobTwoMachine()
    {
        var ops0 = new List<Operation>
        {
            new(0, 0, "MachineA", 2),
            new(0, 1, "MachineB", 3),
        };
        var ops1 = new List<Operation>
        {
            new(1, 0, "MachineA", 4),
            new(1, 1, "MachineB", 1),
        };
        return [new Job(0, ops0), new Job(1, ops1)];
    }

    /// <summary>Single job, single operation on one machine (t=5). Trivial case.</summary>
    private static List<Job> MakeSingleJobSingleOp()
    {
        var ops = new List<Operation> { new(0, 0, "MachineA", 5) };
        return [new Job(0, ops)];
    }

    /// <summary>Two jobs sharing a single machine — all operations serial.</summary>
    private static List<Job> MakeTwoJobsSameMachine()
    {
        var ops0 = new List<Operation>
        {
            new(0, 0, "MachineA", 3),
            new(0, 1, "MachineA", 2),
        };
        var ops1 = new List<Operation>
        {
            new(1, 0, "MachineA", 4),
            new(1, 1, "MachineA", 1),
        };
        return [new Job(0, ops0), new Job(1, ops1)];
    }

    // -------------------------------------------------------------------------
    // FitnessFunction tests (internal access via InternalsVisibleTo)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void FitnessFunction_KnownChromosome_ReturnsExpectedMakespan()
    {
        // Arrange
        // Trace for chromosome [0, 1, 0, 1]:
        //   gene 0 → job0-Op0 (MachineA, t=2): start=max(0,0)=0, finish=2. machA=2, jobT[0]=2
        //   gene 1 → job1-Op0 (MachineA, t=4): start=max(2,0)=2, finish=6. machA=6, jobT[1]=6
        //   gene 2 → job0-Op1 (MachineB, t=3): start=max(0,2)=2, finish=5. machB=5, jobT[0]=5
        //   gene 3 → job1-Op1 (MachineB, t=1): start=max(5,6)=6, finish=7. machB=7, jobT[1]=7
        //   makespan = 7
        var jobs = MakeTwoJobTwoMachine();
        var ga = new GeneticAlgorithm();

        // Act
        int result = ga.FitnessFunction(new[] { 0, 1, 0, 1 }, jobs);

        // Assert
        Assert.AreEqual(7, result);
    }

    [TestMethod]
    public void FitnessFunction_SingleJobSingleOp_ReturnProcessingTime()
    {
        // Arrange: only one operation — makespan must equal its processing time.
        var jobs = MakeSingleJobSingleOp();
        var ga = new GeneticAlgorithm();

        // Act
        int result = ga.FitnessFunction(new[] { 0 }, jobs);

        // Assert
        Assert.AreEqual(5, result);
    }

    [TestMethod]
    public void FitnessFunction_AllOpsOnSameMachine_ReturnsSumOfProcessingTimes()
    {
        // Arrange: both jobs use MachineA → operations are serial regardless of order.
        // Total processing time = 3 + 2 + 4 + 1 = 10.
        var jobs = MakeTwoJobsSameMachine();
        var ga = new GeneticAlgorithm();

        // Act: job precedence forces 0-0→0-1 and 1-0→1-1; machine forces all to queue.
        // For [0,0,1,1]: job0-Op0(3), job0-Op1 must wait until job0-Op0 done (t=3) and machA free.
        // job0-Op0: start=0,finish=3. job0-Op1: start=3,finish=5.
        // job1-Op0: start=5,finish=9. job1-Op1: start=9,finish=10. makespan=10.
        int result = ga.FitnessFunction(new[] { 0, 0, 1, 1 }, jobs);

        // Assert
        Assert.AreEqual(10, result);
    }

    // -------------------------------------------------------------------------
    // Solve tests
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Solve_SingleJobSingleOperation_MakespanEqualsProcessingTime()
    {
        // Arrange
        var jobs = MakeSingleJobSingleOp();
        var config = new AlgorithmConfig
        {
            PopulationSize = 10,
            Generations = 5,
            Repetitions = 1,
            EliteCount = 1,
        };

        // Act
        var schedule = new GeneticAlgorithm().Solve(jobs, config);

        // Assert: only one operation, so makespan must equal its processing time.
        Assert.AreEqual(5, schedule.Makespan);
    }

    [TestMethod]
    public void Solve_TwoJobTwoMachine_ReturnsFeasibleMakespan()
    {
        // Arrange
        var jobs = MakeTwoJobTwoMachine();
        var config = new AlgorithmConfig
        {
            PopulationSize = 20,
            Generations = 30,
            Repetitions = 1,
            EliteCount = 2,
        };

        // Act
        var schedule = new GeneticAlgorithm().Solve(jobs, config);

        // Assert: makespan must be feasible (≥ theoretical lower bound of 7,
        // computed as the minimum possible given machine and job constraints).
        Assert.IsGreaterThanOrEqualTo(7, schedule.Makespan,
            $"Makespan {schedule.Makespan} is below the feasibility lower bound of 7.");
        Assert.IsLessThan(1000, schedule.Makespan,
            $"Makespan {schedule.Makespan} is implausibly large.");
    }

    [TestMethod]
    public void Solve_FiresOnConvergenceOncePerGeneration()
    {
        // Arrange
        var jobs = MakeTwoJobTwoMachine();
        var config = new AlgorithmConfig
        {
            PopulationSize = 10,
            Generations = 5,
            Repetitions = 1,
            EliteCount = 1,
        };
        var ga = new GeneticAlgorithm();
        int eventCount = 0;
        ga.OnConvergence += (_, _) => eventCount++;

        // Act
        ga.Solve(jobs, config);

        // Assert: exactly Generations events fired (one per generation per repetition).
        Assert.AreEqual(config.Generations, eventCount);
    }

    [TestMethod]
    public void GetStats_AfterSolve_BestMakespanLessThanOrEqualMean()
    {
        // Arrange
        var jobs = MakeTwoJobTwoMachine();
        var config = new AlgorithmConfig
        {
            PopulationSize = 10,
            Generations = 5,
            Repetitions = 3,
            EliteCount = 1,
        };
        var ga = new GeneticAlgorithm();

        // Act
        ga.Solve(jobs, config);
        var stats = ga.GetStats();

        // Assert: best ≤ mean is always true by definition; also non-null.
        Assert.IsNotNull(stats);
        Assert.IsLessThanOrEqualTo(stats.MeanMakespan, (double)stats.BestMakespan,
            $"Best {stats.BestMakespan} should be ≤ mean {stats.MeanMakespan}.");
    }

    [TestMethod]
    public void GetStats_BeforeSolve_ReturnsNull()
    {
        // Arrange & Act
        var stats = new GeneticAlgorithm().GetStats();

        // Assert
        Assert.IsNull(stats);
    }
}
