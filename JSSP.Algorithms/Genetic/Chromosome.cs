using JSSP.Core.Models;

namespace JSSP.Algorithms.Genetic;

/// <summary>
/// Immutable job-based chromosome for the JSSP genetic algorithm.
/// Each gene is a job index; job index <c>j</c> appears exactly as many times
/// as job <c>j</c> has operations. This encoding guarantees feasibility —
/// no repair operator is ever required.
/// </summary>
internal sealed class Chromosome
{
    private readonly int[] _genes;

    /// <summary>Gets the number of genes (total operations across all jobs).</summary>
    public int Length => _genes.Length;

    /// <summary>Gets the gene at position <paramref name="index"/>.</summary>
    public int this[int index] => _genes[index];

    /// <summary>
    /// Gets a string key suitable for use as a <see cref="FitnessCache"/> lookup key.
    /// Format: comma-separated gene values.
    /// </summary>
    public string HashKey { get; }

    private Chromosome(int[] genes)
    {
        _genes = genes;
        HashKey = string.Join(",", genes);
    }

    /// <summary>Returns a defensive copy of the internal gene array.</summary>
    public int[] ToArray() => (int[])_genes.Clone();

    /// <summary>
    /// Creates a new <see cref="Chromosome"/> from an existing gene array.
    /// The array is copied defensively so the caller's array is never mutated.
    /// </summary>
    /// <param name="genes">Gene array to wrap.</param>
    /// <returns>A new chromosome backed by a copy of <paramref name="genes"/>.</returns>
    public static Chromosome FromArray(int[] genes) => new((int[])genes.Clone());

    /// <summary>
    /// Creates a randomly shuffled chromosome for the given job list using
    /// a Fisher-Yates shuffle. Each job index <c>j</c> appears exactly
    /// <c>jobs[j].Operations.Count</c> times.
    /// </summary>
    /// <param name="jobs">Job list defining how many times each job index appears.</param>
    /// <param name="rng">Random number generator controlling the shuffle.</param>
    /// <returns>A new randomly ordered chromosome.</returns>
    public static Chromosome CreateRandom(List<Job> jobs, Random rng)
    {
        var genes = BuildGenePool(jobs);
        Shuffle(genes, rng);
        return new Chromosome(genes);
    }

    /// <summary>
    /// Builds an unshuffled pool containing each job index repeated once per operation.
    /// </summary>
    private static int[] BuildGenePool(List<Job> jobs)
    {
        int totalOps = jobs.Sum(j => j.Operations.Count);
        var pool = new int[totalOps];
        int idx = 0;
        foreach (var job in jobs)
            for (int k = 0; k < job.Operations.Count; k++)
                pool[idx++] = job.Id;
        return pool;
    }

    /// <summary>Fisher-Yates in-place shuffle.</summary>
    private static void Shuffle(int[] array, Random rng)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (array[i], array[j]) = (array[j], array[i]);
        }
    }
}
