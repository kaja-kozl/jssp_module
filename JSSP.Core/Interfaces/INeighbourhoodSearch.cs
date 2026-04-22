using JSSP.Core.Models;

namespace JSSP.Core.Interfaces;

/// <summary>
/// Generates neighbouring schedules from a current solution for use in local search.
/// </summary>
public interface INeighbourhoodSearch
{
    /// <summary>
    /// Produces all valid neighbours of the given schedule.
    /// </summary>
    /// <param name="current">The current schedule to generate neighbours from.</param>
    /// <returns>Sequence of neighbouring schedules.</returns>
    IEnumerable<Schedule> GetNeighbours(Schedule current);
}
