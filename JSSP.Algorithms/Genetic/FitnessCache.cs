namespace JSSP.Algorithms.Genetic;

/// <summary>
/// Thread-safe cache for chromosome fitness (makespan) values.
/// Key: <see cref="Chromosome.HashKey"/>. Value: makespan.
/// Eliminates redundant makespan recalculations when the same chromosome
/// reappears across generations — common in converging populations.
/// The lock is per-instance so parallel population evaluation is safe.
/// </summary>
internal sealed class FitnessCache
{
    private readonly Dictionary<string, int> _cache = new();
    private readonly object _lock = new();

    /// <summary>
    /// Attempts to retrieve a cached makespan for the given chromosome key.
    /// </summary>
    /// <param name="key">The chromosome's <see cref="Chromosome.HashKey"/>.</param>
    /// <param name="fitness">Receives the cached makespan if found.</param>
    /// <returns><c>true</c> if a cached value was found; otherwise <c>false</c>.</returns>
    public bool TryGet(string key, out int fitness)
    {
        lock (_lock)
            return _cache.TryGetValue(key, out fitness);
    }

    /// <summary>Stores a makespan value for the given chromosome key.</summary>
    /// <param name="key">The chromosome's <see cref="Chromosome.HashKey"/>.</param>
    /// <param name="fitness">The makespan value to cache.</param>
    public void Store(string key, int fitness)
    {
        lock (_lock)
            _cache[key] = fitness;
    }
}
