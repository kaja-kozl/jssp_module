using JSSP.Core.Interfaces;
using JSSP.Core.Models;

namespace JSSP.Algorithms.HyperParameter;

/// <summary>
/// Random search: samples <paramref name="sampleCount"/> configurations from the
/// full hyperparameter grid without replacement and returns the best. Substantially
/// faster than <see cref="GridSearch"/> for large grids at the cost of coverage.
/// <para>
/// Per the random search literature (Bergstra &amp; Bengio, 2012): for grids where
/// most parameters have little effect, random search finds equally good results in
/// far fewer evaluations than exhaustive grid search.
/// </para>
/// </summary>
public static class RandomSearch
{
    /// <summary>
    /// Randomly samples and evaluates configurations from the hyperparameter grid.
    /// </summary>
    /// <param name="algorithmFactory">
    /// Delegate that produces a fresh algorithm instance for each trial.
    /// </param>
    /// <param name="jobs">The problem instance to optimise against.</param>
    /// <param name="sampleCount">Number of random configurations to evaluate.</param>
    /// <param name="rng">Random source for sampling.</param>
    /// <param name="cancellationToken">Token that halts the search early.</param>
    /// <param name="populationSizes">Override for the population size axis.</param>
    /// <param name="mutationRates">Override for the mutation rate axis.</param>
    /// <param name="tournamentSizes">Override for the tournament size axis.</param>
    /// <param name="tabuListLengths">Override for the tabu list length axis.</param>
    /// <param name="injectionFrequencies">Override for the injection frequency axis.</param>
    /// <param name="generations">Generations per trial run.</param>
    /// <param name="repetitions">Repetitions per trial run.</param>
    /// <returns>
    /// A <see cref="HyperparameterResult"/> with the best config found among the sampled set.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no configurations were evaluated.
    /// </exception>
    public static HyperparameterResult Run(
        Func<IAlgorithm> algorithmFactory,
        List<Job> jobs,
        int sampleCount,
        Random? rng = null,
        CancellationToken cancellationToken = default,
        IReadOnlyList<int>? populationSizes = null,
        IReadOnlyList<double>? mutationRates = null,
        IReadOnlyList<int>? tournamentSizes = null,
        IReadOnlyList<int>? tabuListLengths = null,
        IReadOnlyList<int>? injectionFrequencies = null,
        int generations = 100,
        int repetitions = 3)
    {
        rng ??= new Random();

        var samples = ParameterGrid.Sample(
            sampleCount, rng,
            populationSizes, mutationRates, tournamentSizes,
            tabuListLengths, injectionFrequencies,
            generations, repetitions);

        AlgorithmConfig? bestConfig = null;
        AlgorithmStats? bestStats = null;
        int bestMakespan = int.MaxValue;
        int evaluated = 0;

        foreach (var config in samples)
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
                "Random search evaluated no configurations. " +
                "Check sampleCount > 0 and that cancellation was not requested immediately.");

        return new HyperparameterResult(bestConfig, bestStats, evaluated);
    }
}
