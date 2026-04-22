using JSSP.Core.Models;

namespace JSSP.Core.Validation;

/// <summary>
/// Validates that a schedule satisfies all four JSSP hard constraints:
/// <list type="number">
///   <item>Each operation starts only after the previous operation in the same job completes (precedence).</item>
///   <item>Each machine processes at most one operation at a time (machine exclusivity).</item>
///   <item>Completion time equals start time plus processing time — no negative start times (exact timing).</item>
///   <item>Operations cannot be preempted once started — every operation in the job list must appear exactly once in the schedule (completeness).</item>
/// </list>
/// </summary>
public class ScheduleValidator
{
    /// <summary>
    /// Returns <see langword="true"/> when the schedule satisfies all four hard constraints.
    /// </summary>
    /// <param name="schedule">The schedule to validate.</param>
    /// <param name="jobs">The original job list used to build the schedule.</param>
    public bool IsValid(Schedule schedule, List<Job> jobs) =>
        Validate(schedule, jobs).IsValid;

    /// <summary>
    /// Checks all four hard constraints and returns a <see cref="ValidationResult"/>
    /// containing every violation found (not just the first).
    /// Violations are reported in constraint order: C3 → C4 → C1 → C2.
    /// </summary>
    /// <param name="schedule">The schedule to validate.</param>
    /// <param name="jobs">The original job list used to build the schedule.</param>
    /// <returns>
    /// A <see cref="ValidationResult"/> with <see cref="ValidationResult.IsValid"/> set to
    /// <see langword="true"/> and an empty <see cref="ValidationResult.Violations"/> list
    /// when the schedule is feasible.
    /// </returns>
    public ValidationResult Validate(Schedule schedule, List<Job> jobs)
    {
        var violations = new List<string>();

        CheckNonNegativeStartTimes(schedule, jobs, violations);   // Constraint 3
        CheckCompleteness(schedule, jobs, violations);             // Constraint 4
        CheckJobPrecedence(schedule, jobs, violations);            // Constraint 1
        CheckMachineExclusivity(schedule, jobs, violations);       // Constraint 2

        return new ValidationResult(violations.Count == 0, violations.AsReadOnly());
    }

    // -------------------------------------------------------------------------
    // Constraint 3 — Non-negative start times
    // Completion time = start time + processing time is only well-defined when
    // start time >= 0. A negative start time implies the schedule is internally
    // inconsistent.
    // -------------------------------------------------------------------------

    private static void CheckNonNegativeStartTimes(
        Schedule schedule,
        List<Job> jobs,
        List<string> violations)
    {
        foreach (Job job in jobs)
        {
            foreach (Operation op in job.Operations)
            {
                int start = schedule.GetStartTime(job.Id, op.OperationId);
                if (start < 0)
                {
                    violations.Add(
                        $"C3 — Job {job.Id} Op {op.OperationId}: " +
                        $"start time {start} is negative. " +
                        $"Completion time must equal start + processing time ({op.ProcessingTime}).");
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Constraint 4 — Completeness (non-preemption)
    // Every operation in the job list must have an explicit start time in the
    // schedule. A missing entry means the operation was never scheduled,
    // violating the requirement that each operation runs to completion as one
    // uninterrupted block.
    // -------------------------------------------------------------------------

    private static void CheckCompleteness(
        Schedule schedule,
        List<Job> jobs,
        List<string> violations)
    {
        foreach (Job job in jobs)
        {
            foreach (Operation op in job.Operations)
            {
                if (!schedule.ContainsOperation(job.Id, op.OperationId))
                {
                    violations.Add(
                        $"C4 — Job {job.Id} Op {op.OperationId} " +
                        $"(machine '{op.Subdivision}'): " +
                        "operation has no start time in the schedule — it was never scheduled.");
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Constraint 1 — Job precedence
    // Within a job, operation k may not begin until operation k-1 has finished.
    // Required start time >= previous start + previous processing time.
    // -------------------------------------------------------------------------

    private static void CheckJobPrecedence(
        Schedule schedule,
        List<Job> jobs,
        List<string> violations)
    {
        foreach (Job job in jobs)
        {
            for (int k = 1; k < job.Operations.Count; k++)
            {
                Operation prev = job.Operations[k - 1];
                Operation curr = job.Operations[k];

                int prevStart = schedule.GetStartTime(job.Id, prev.OperationId);
                int currStart = schedule.GetStartTime(job.Id, curr.OperationId);
                int prevCompletion = prevStart + prev.ProcessingTime;

                if (currStart < prevCompletion)
                {
                    violations.Add(
                        $"C1 — Job {job.Id}: Op {curr.OperationId} starts at t={currStart} " +
                        $"but Op {prev.OperationId} does not complete until t={prevCompletion} " +
                        $"(start={prevStart} + processing={prev.ProcessingTime}). " +
                        "Precedence constraint violated.");
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Constraint 2 — Machine exclusivity
    // At most one operation may run on a given machine at any time.
    // Operations are grouped by machine (Subdivision), sorted by start time,
    // then each consecutive pair is checked for overlap.
    // Two intervals [a, a+pa) and [b, b+pb) overlap when b < a + pa
    // (assuming a <= b after sorting).
    // -------------------------------------------------------------------------

    private static void CheckMachineExclusivity(
        Schedule schedule,
        List<Job> jobs,
        List<string> violations)
    {
        // Build a flat list of (machine, startTime, completionTime, label) tuples.
        var byMachine = new Dictionary<string, List<(int start, int completion, string label)>>();

        foreach (Job job in jobs)
        {
            foreach (Operation op in job.Operations)
            {
                int start = schedule.GetStartTime(job.Id, op.OperationId);
                int completion = start + op.ProcessingTime;
                string label = $"Job {job.Id} Op {op.OperationId}";

                if (!byMachine.ContainsKey(op.Subdivision))
                    byMachine[op.Subdivision] = [];

                byMachine[op.Subdivision].Add((start, completion, label));
            }
        }

        foreach ((string machine, var ops) in byMachine)
        {
            // Sort by start time so we only need to check consecutive pairs.
            ops.Sort((a, b) => a.start.CompareTo(b.start));

            for (int i = 0; i < ops.Count - 1; i++)
            {
                var (aStart, aCompletion, aLabel) = ops[i];
                var (bStart, _, bLabel) = ops[i + 1];

                // Touching intervals (bStart == aCompletion) are valid.
                if (bStart < aCompletion)
                {
                    violations.Add(
                        $"C2 — Machine '{machine}': {aLabel} occupies [{aStart},{aCompletion}) " +
                        $"and {bLabel} starts at t={bStart}. " +
                        "Machine exclusivity violated — overlapping intervals.");
                }
            }
        }
    }
}
