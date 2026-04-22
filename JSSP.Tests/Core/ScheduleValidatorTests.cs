using JSSP.Core.Models;
using JSSP.Core.Validation;

namespace JSSP.Tests.Core;

/// <summary>
/// Unit tests for <see cref="ScheduleValidator"/> covering all four JSSP hard constraints.
///
/// Fixture — two jobs, two machines:
///   Job 1: Op1 → "Machining" (proc=3),  Op2 → "Welding"   (proc=2)
///   Job 2: Op1 → "Welding"   (proc=4),  Op2 → "Machining" (proc=2)
///
/// Canonical valid assignment (no overlaps, precedence respected):
///   J1O1 = t=0  [Machining 0–3)
///   J1O2 = t=3  [Welding   3–5)
///   J2O1 = t=0  [Welding   0–4)    ← Welding: J2O1[0,4) then J1O2[3,5) — wait, overlap!
///
/// Corrected valid assignment:
///   J1O1 = t=0  [Machining 0–3)
///   J2O2 = t=3  [Machining 3–5)    ← Machining: J1O1[0,3) then J2O2[3,5) — adjacent, OK
///   J2O1 = t=0  [Welding   0–4)
///   J1O2 = t=4  [Welding   4–6)    ← Welding:   J2O1[0,4) then J1O2[4,6) — adjacent, OK
///   Precedence J1: Op1 ends t=3, Op2 starts t=4 ✓
///   Precedence J2: Op1 ends t=4, Op2 starts t=3 — WRONG, use t=4 for J2O2
///
/// Final valid assignment used in tests:
///   J1O1 = t=0, J1O2 = t=4   (Job 1 precedence: 0+3=3 <= 4 ✓)
///   J2O1 = t=0, J2O2 = t=4   (Job 2 precedence: 0+4=4 <= 4 ✓)
///   Machining: J1O1[0,3) then J2O2[4,6) — gap is fine, no overlap ✓
///   Welding:   J2O1[0,4) then J1O2[4,6) — adjacent ✓
/// </summary>
[TestClass]
public class ScheduleValidatorTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (List<Job> jobs, Schedule schedule) BuildValidFixture()
    {
        var jobs = new List<Job>
        {
            new(1, new List<Operation>
            {
                new(1, 1, "Machining", 3),
                new(1, 2, "Welding", 2),
            }.AsReadOnly()),
            new(2, new List<Operation>
            {
                new(2, 1, "Welding", 4),
                new(2, 2, "Machining", 2),
            }.AsReadOnly()),
        };

        var schedule = new Schedule();
        schedule.SetStartTime(1, 1, 0, 3);  // J1O1: Machining [0, 3)
        schedule.SetStartTime(1, 2, 4, 2);  // J1O2: Welding   [4, 6)
        schedule.SetStartTime(2, 1, 0, 4);  // J2O1: Welding   [0, 4)
        schedule.SetStartTime(2, 2, 4, 2);  // J2O2: Machining [4, 6)

        return (jobs, schedule);
    }

    private static ScheduleValidator Validator() => new();

    // -------------------------------------------------------------------------
    // Happy-path tests
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Validate_ReturnsValidForFeasibleSchedule()
    {
        // Arrange
        var (jobs, schedule) = BuildValidFixture();

        // Act
        ValidationResult result = Validator().Validate(schedule, jobs);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsEmpty(result.Violations);
    }

    [TestMethod]
    public void IsValid_ReturnsTrueForFeasibleSchedule()
    {
        // Arrange
        var (jobs, schedule) = BuildValidFixture();

        // Act
        bool valid = Validator().IsValid(schedule, jobs);

        // Assert
        Assert.IsTrue(valid);
    }

    [TestMethod]
    public void Validate_SingleJobSingleOperation_IsValid()
    {
        // Arrange
        var jobs = new List<Job>
        {
            new(1, new List<Operation> { new(1, 1, "Machining", 5) }.AsReadOnly()),
        };
        var schedule = new Schedule();
        schedule.SetStartTime(1, 1, 0, 5);

        // Act
        ValidationResult result = Validator().Validate(schedule, jobs);

        // Assert
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void Validate_EmptyJobList_IsValid()
    {
        // Arrange — vacuously valid
        var jobs = new List<Job>();
        var schedule = new Schedule();

        // Act
        ValidationResult result = Validator().Validate(schedule, jobs);

        // Assert
        Assert.IsTrue(result.IsValid);
        Assert.IsEmpty(result.Violations);
    }

    [TestMethod]
    public void Validate_AdjacentMachineIntervals_AreNotAnOverlap()
    {
        // Arrange — two ops on Machining: [0,3) and [3,5). End of A == start of B is valid.
        var jobs = new List<Job>
        {
            new(1, new List<Operation> { new(1, 1, "Machining", 3) }.AsReadOnly()),
            new(2, new List<Operation> { new(2, 1, "Machining", 2) }.AsReadOnly()),
        };
        var schedule = new Schedule();
        schedule.SetStartTime(1, 1, 0, 3);
        schedule.SetStartTime(2, 1, 3, 2);  // starts exactly when J1O1 finishes

        // Act
        ValidationResult result = Validator().Validate(schedule, jobs);

        // Assert
        Assert.IsTrue(result.IsValid);
    }

    // -------------------------------------------------------------------------
    // Constraint 1 — Job precedence
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Validate_C1_ViolatesJobPrecedence()
    {
        // Arrange — J1O2 starts at t=1, but J1O1 doesn't finish until t=3
        var (jobs, _) = BuildValidFixture();
        var schedule = new Schedule();
        schedule.SetStartTime(1, 1, 0, 3);  // J1O1 finishes t=3
        schedule.SetStartTime(1, 2, 1, 2);  // J1O2 starts t=1 — too early ✗
        schedule.SetStartTime(2, 1, 0, 4);
        schedule.SetStartTime(2, 2, 4, 2);

        // Act
        ValidationResult result = Validator().Validate(schedule, jobs);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.IsGreaterThanOrEqualTo(1, result.Violations.Count);
    }

    [TestMethod]
    public void Validate_C1_ReportsAllPrecedenceViolations()
    {
        // Arrange — both jobs violate precedence
        var (jobs, _) = BuildValidFixture();
        var schedule = new Schedule();
        schedule.SetStartTime(1, 1, 0, 3);
        schedule.SetStartTime(1, 2, 1, 2);  // J1O2 too early ✗
        schedule.SetStartTime(2, 1, 0, 4);
        schedule.SetStartTime(2, 2, 2, 2);  // J2O2 too early ✗

        // Act
        ValidationResult result = Validator().Validate(schedule, jobs);

        // Assert — both violations reported
        Assert.IsFalse(result.IsValid);
        Assert.IsGreaterThanOrEqualTo(2, result.Violations.Count);
    }

    // -------------------------------------------------------------------------
    // Constraint 2 — Machine exclusivity
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Validate_C2_ViolatesMachineExclusivity()
    {
        // Arrange — Welding: J2O1[0,4) overlaps J1O2[1,3)
        var (jobs, _) = BuildValidFixture();
        var schedule = new Schedule();
        schedule.SetStartTime(1, 1, 0, 3);
        schedule.SetStartTime(1, 2, 1, 2);  // Welding [1, 3) — overlaps J2O1 ✗
        schedule.SetStartTime(2, 1, 0, 4);  // Welding [0, 4)
        schedule.SetStartTime(2, 2, 4, 2);

        // Act
        ValidationResult result = Validator().Validate(schedule, jobs);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.IsGreaterThanOrEqualTo(1, result.Violations.Count);
    }

    // -------------------------------------------------------------------------
    // Constraint 3 — Non-negative start times
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Validate_C3_ViolatesNonNegativeStart()
    {
        // Arrange — J1O1 given a negative start time
        var (jobs, _) = BuildValidFixture();
        var schedule = new Schedule();
        schedule.SetStartTime(1, 1, -1, 3);  // negative ✗
        schedule.SetStartTime(1, 2, 4, 2);
        schedule.SetStartTime(2, 1, 0, 4);
        schedule.SetStartTime(2, 2, 4, 2);

        // Act
        ValidationResult result = Validator().Validate(schedule, jobs);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.IsGreaterThanOrEqualTo(1, result.Violations.Count);
    }

    // -------------------------------------------------------------------------
    // Constraint 4 — Completeness (non-preemption)
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Validate_C4_ViolatesCompleteness_WhenOperationMissing()
    {
        // Arrange — J2O2 is never added to the schedule
        var (jobs, _) = BuildValidFixture();
        var schedule = new Schedule();
        schedule.SetStartTime(1, 1, 0, 3);
        schedule.SetStartTime(1, 2, 4, 2);
        schedule.SetStartTime(2, 1, 0, 4);
        // J2O2 intentionally omitted ✗

        // Act
        ValidationResult result = Validator().Validate(schedule, jobs);

        // Assert
        Assert.IsFalse(result.IsValid);
        Assert.IsGreaterThanOrEqualTo(1, result.Violations.Count);
    }

    [TestMethod]
    public void Validate_C4_StartTimeZeroIsNotTreatedAsMissing()
    {
        // Arrange — J1O1 legitimately starts at t=0; must NOT be flagged as missing
        var jobs = new List<Job>
        {
            new(1, new List<Operation>
            {
                new(1, 1, "Machining", 3),
                new(1, 2, "Welding", 2),
            }.AsReadOnly()),
        };
        var schedule = new Schedule();
        schedule.SetStartTime(1, 1, 0, 3);  // start=0 is valid
        schedule.SetStartTime(1, 2, 3, 2);

        // Act
        ValidationResult result = Validator().Validate(schedule, jobs);

        // Assert — t=0 must not be mistaken for "not set"
        Assert.IsTrue(result.IsValid);
        Assert.IsEmpty(result.Violations);
    }
}
