using JSSP.Core.Models;

namespace JSSP.Core.Interfaces;

/// <summary>
/// Evaluates the fitness (makespan) of a candidate schedule.
/// </summary>
public interface IFitnessEvaluator
{
    /// <summary>
    /// Returns the makespan of the given schedule. Lower is better.
    /// </summary>
    /// <param name="schedule">The schedule to evaluate.</param>
    /// <returns>Makespan value.</returns>
    int Evaluate(Schedule schedule);
}
