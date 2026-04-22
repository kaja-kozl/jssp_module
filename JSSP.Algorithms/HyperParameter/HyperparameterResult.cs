using JSSP.Core.Models;

namespace JSSP.Algorithms.HyperParameter;

/// <summary>
/// Holds the best configuration and its statistics returned by a hyperparameter search.
/// </summary>
public sealed class HyperparameterResult
{
    /// <summary>Gets the hyperparameter configuration that produced the best makespan.</summary>
    public AlgorithmConfig BestConfig { get; }

    /// <summary>Gets the statistical results produced by running <see cref="BestConfig"/>.</summary>
    public AlgorithmStats BestStats { get; }

    /// <summary>Gets the total number of configurations evaluated during the search.</summary>
    public int ConfigsEvaluated { get; }

    /// <summary>Initialises a new <see cref="HyperparameterResult"/>.</summary>
    public HyperparameterResult(AlgorithmConfig bestConfig, AlgorithmStats bestStats, int configsEvaluated)
    {
        BestConfig = bestConfig;
        BestStats = bestStats;
        ConfigsEvaluated = configsEvaluated;
    }
}
