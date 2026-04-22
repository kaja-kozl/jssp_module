using JSSP.Core.Models;

namespace JSSP.Core.Interfaces;

/// <summary>
/// Contract for all JSSP optimisation algorithms.
/// Implementations are discovered and instantiated via <c>AlgorithmFactory</c>.
/// </summary>
public interface IAlgorithm
{
    /// <summary>
    /// Runs the algorithm and returns the best schedule found.
    /// </summary>
    /// <param name="jobs">The list of jobs to schedule.</param>
    /// <param name="config">Hyperparameter configuration for this run.</param>
    /// <returns>The best feasible <see cref="Schedule"/> found.</returns>
    Schedule Solve(List<Job> jobs, AlgorithmConfig config);

    /// <summary>
    /// Fires after each generation or iteration with the current best makespan.
    /// Subscribers that update Terminal.Gui views must marshal back to the UI thread
    /// via <c>Application.Invoke()</c> before touching any view.
    /// </summary>
    event EventHandler<ConvergenceEventArgs> OnConvergence;

    /// <summary>
    /// Returns the statistical summary of the most recently completed run,
    /// or <c>null</c> if <see cref="Solve"/> has not yet been called.
    /// </summary>
    AlgorithmStats? GetStats();
}
