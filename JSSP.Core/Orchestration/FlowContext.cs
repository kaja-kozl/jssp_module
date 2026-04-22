using JSSP.Core.Models;

namespace JSSP.Core.Orchestration;

/// <summary>
/// Shared state bag passed through every node of the application flow graph.
/// Replaces the former static <c>AppState</c> class.
/// </summary>
public class FlowContext
{
    /// <summary>Gets or sets the jobs loaded from the selected CSV file.</summary>
    public List<Job>? Jobs { get; set; }

    /// <summary>Gets or sets the name of the algorithm selected by the user.</summary>
    public string? SelectedAlgorithmName { get; set; }

    /// <summary>Gets or sets the best schedule found by the most recent algorithm run.</summary>
    public Schedule? BestSchedule { get; set; }

    /// <summary>Gets or sets the statistical results of the most recent algorithm run.</summary>
    public AlgorithmStats? LastStats { get; set; }

    /// <summary>Gets or sets the hyperparameter configuration for the next algorithm run.</summary>
    public AlgorithmConfig Config { get; set; } = new();

    /// <summary>
    /// Gets or sets the per-algorithm results from the most recent "compare all" run.
    /// <see langword="null"/> until a comparison run has been completed.
    /// </summary>
    public List<AlgorithmComparisonResult>? CompareResults { get; set; }
}
