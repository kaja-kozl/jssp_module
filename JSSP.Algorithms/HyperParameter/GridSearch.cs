using JSSP.Core.Interfaces;
using JSSP.Core.Models;

namespace JSSP.Algorithms.HyperParameter;

/// <summary>
/// Exhaustive grid search: evaluates every configuration in the Cartesian product
/// of <see cref="ParameterGrid"/> and returns the one that achieves the lowest
/// best makespan on the supplied job list.
/// <para>
/// Grid size with default axes: 7 × 10 × 5 × 4 × 5 = 7 000 configurations.
/// Consider using <see cref="RandomSearch"/> for faster approximate tuning.
/// </para>
/// </summary>
public static class GridSearch
{
    /// <summary>
    /// Evaluates every configuration in the hyperparameter grid and returns the best.
    /// </summary>
    /// <param name="algorithmFactory">
    /// Delegate that produces a fresh algorithm instance for each trial. A new instance
    /// must be created per trial so that internal state (fitness cache, convergence event)
    /// does not bleed across evaluations.
    /// </param>
    /// <param name="jobs">The problem instance to optimise against.</param>
    /// <param name="cancellationToken">
    /// Token that halts the search early. The best result found so far is returned.
    /// </param>
    /// <param name="populationSizes">Override for the population size axis.</param>
    /// <param name="mutationRates">Override for the mutation rate axis.</param>
    /// <param name="tournamentSizes">Override for the tournament size axis.</param>
    /// <param name="tabuListLengths">Override for the tabu list length axis.</param>
    /// <param name="injectionFrequencies">Override for the injection frequency axis.</param>
    /// <param name="generations">Generations per trial run.</param>
    /// <param name="repetitions">Repetitions per trial run.</param>
    /// <returns>
    /// A <see cref="HyperparameterResult"/> containing the best configuration, its stats,
    /// and the count of configurations evaluated (may be less than the full grid if cancelled).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no configurations were evaluated (empty grid or immediate cancellation).
    /// </exception>
    public static HyperparameterResult Run(
        Func<IAlgorithm> algorithmFactory,
        List<Job> jobs,
        CancellationToken cancellationToken = default,
        IReadOnlyList<int>? populationSizes = null,
        IReadOnlyList<double>? mutationRates = null,
        IReadOnlyList<int>? tournamentSizes = null,
        IReadOnlyList<int>? tabuListLengths = null,
        IReadOnlyList<int>? injectionFrequencies = null,
        int generations = 100,
        int repetitions = 3)
    {
        AlgorithmConfig? bestConfig = null;
        AlgorithmStats? bestStats = null;
        int bestMakespan = int.MaxValue;
        int evaluated = 0;

        foreach (var config in ParameterGrid.Enumerate(
                     populationSizes, mutationRates, tournamentSizes,
                     tabuListLengths, injectionFrequencies,
                     generations, repetitions))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var trial = algorithmFactory();
            var configWithCt = new AlgorithmConfig
            {
                PopulationSize = config.PopulationSize,
                MutationRate = config.MutationRate,
                TournamentSize = config.TournamentSize,
                TabuListLength = config.TabuListLength,
                InjectionFrequencyK = config.InjectionFrequencyK,
                Generations = config.Generations,
                Repetitions = config.Repetitions,
                EliteCount = config.EliteCount,
                CancellationToken = cancellationToken,
            };

            trial.Solve(jobs, configWithCt);
            var stats = trial.GetStats();
            evaluated++;

            if (stats is not null && stats.BestMakespan < bestMakespan)
            {
                bestMakespan = stats.BestMakespan;
                bestConfig = configWithCt;
                bestStats = stats;
            }
        }

        if (bestConfig is null || bestStats is null)
            throw new InvalidOperationException(
                "Grid search evaluated no configurations. Check that the parameter grid is non-empty " +
                "and that cancellation was not requested before the first trial.");

        return new HyperparameterResult(bestConfig, bestStats, evaluated);
    }
}
