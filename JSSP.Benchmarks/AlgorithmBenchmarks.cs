using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using JSSP.Algorithms.Genetic;
using JSSP.Core.Models;
using JSSP.IO;

// Entry point: BenchmarkDotNet discovers [Benchmark] methods and runs them in Release mode.
// DO NOT run this project in Debug — BenchmarkDotNet will reject it.
BenchmarkRunner.Run<AlgorithmBenchmarks>();

/// <summary>
/// Benchmarks for the three performance-critical paths identified in CLAUDE.md A9:
/// fitness function (makespan calculation), full GA solve, and CSV parsing.
/// </summary>
/// <remarks>
/// Targets from CLAUDE.md A9:
/// <list type="bullet">
///   <item>Fitness function (small instance): &lt; 1 ms</item>
///   <item>Fitness function (large instance): &lt; 10 ms</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
public class AlgorithmBenchmarks
{
    private GeneticAlgorithm _ga = null!;
    private List<Job> _smallJobs = null!;
    private List<Job> _largeJobs = null!;
    private int[] _smallGenes = null!;
    private int[] _largeGenes = null!;
    private AlgorithmConfig _smallConfig = null!;
    private string _csvPath = null!;

    /// <summary>
    /// Sets up hardcoded instances. CSV file is written to a temp path for the
    /// <see cref="CsvParse"/> benchmark.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _ga = new GeneticAlgorithm();

        // Small instance: 3 jobs × 3 ops = 9 total operations (maps to jobs_small).
        _smallJobs = BuildJobs(jobCount: 3, opsPerJob: 3);
        _smallGenes = BuildGenes(_smallJobs);

        // Large instance: 10 jobs × 10 ops = 100 total operations (approaches jobs_huge scale).
        _largeJobs = BuildJobs(jobCount: 10, opsPerJob: 10);
        _largeGenes = BuildGenes(_largeJobs);

        _smallConfig = new AlgorithmConfig
        {
            PopulationSize = 20,
            Generations = 10,
            Repetitions = 1,
        };

        // Build a minimal CSV for the CSV-parse benchmark.
        _csvPath = Path.Combine(Path.GetTempPath(), $"jssp_bench_{Guid.NewGuid():N}.csv");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("JobId,OperationId,Subdivision,ProcessingTime");
        foreach (var job in _smallJobs)
            foreach (var op in job.Operations)
                sb.AppendLine($"{op.JobId},{op.OperationId},{op.Subdivision},{op.ProcessingTime}");
        File.WriteAllText(_csvPath, sb.ToString());
    }

    /// <summary>Removes the temporary CSV file created in <see cref="Setup"/>.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_csvPath))
            File.Delete(_csvPath);
    }

    // -------------------------------------------------------------------------
    // Benchmark: fitness function (makespan calculation)
    // -------------------------------------------------------------------------

    /// <summary>Measures makespan calculation for a small instance (target: &lt; 1 ms).</summary>
    [Benchmark(Description = "FitnessFunction (small, 9 ops)")]
    public int FitnessSmall() => _ga.FitnessFunction(_smallGenes, _smallJobs);

    /// <summary>Measures makespan calculation for a large instance (target: &lt; 10 ms).</summary>
    [Benchmark(Description = "FitnessFunction (large, 100 ops)")]
    public int FitnessLarge() => _ga.FitnessFunction(_largeGenes, _largeJobs);

    // -------------------------------------------------------------------------
    // Benchmark: full GA solve (includes population eval + generational loop)
    // -------------------------------------------------------------------------

    /// <summary>Measures a short GA run on the small instance.</summary>
    [Benchmark(Description = "GeneticAlgorithm.Solve (small, 20 pop, 10 gen)")]
    public Schedule GaSolveSmall() => _ga.Solve(_smallJobs, _smallConfig);

    // -------------------------------------------------------------------------
    // Benchmark: CSV parsing
    // -------------------------------------------------------------------------

    /// <summary>Measures synchronous CSV loading for the small instance file.</summary>
    [Benchmark(Description = "CsvLoader.Load (small, 9 rows)")]
    public List<Job> CsvParse() => CsvLoader.Load(_csvPath);

    // -------------------------------------------------------------------------
    // Helpers — build hardcoded problem instances
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a synthetic instance with <paramref name="jobCount"/> jobs each having
    /// <paramref name="opsPerJob"/> operations. Machines cycle round-robin (M1..Mn).
    /// Processing times are deterministic (2 × jobId + opId) to avoid random variance.
    /// </summary>
    private static List<Job> BuildJobs(int jobCount, int opsPerJob)
    {
        var jobs = new List<Job>(jobCount);
        for (int j = 1; j <= jobCount; j++)
        {
            var ops = new List<Operation>(opsPerJob);
            for (int o = 1; o <= opsPerJob; o++)
            {
                string machine = $"M{((j + o - 2) % opsPerJob) + 1}";
                int processingTime = 2 * j + o;
                ops.Add(new Operation(j, o, machine, processingTime));
            }
            jobs.Add(new Job(j, ops.AsReadOnly()));
        }
        return jobs;
    }

    /// <summary>
    /// Builds a simple left-to-right gene array: all ops of job 1, then job 2, etc.
    /// This is a valid (though not optimal) encoding for benchmark timing purposes.
    /// </summary>
    private static int[] BuildGenes(List<Job> jobs)
    {
        var genes = new List<int>(jobs.Sum(j => j.Operations.Count));
        foreach (var job in jobs)
            for (int k = 0; k < job.Operations.Count; k++)
                genes.Add(job.Id);
        return genes.ToArray();
    }
}
