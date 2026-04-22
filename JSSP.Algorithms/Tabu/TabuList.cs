namespace JSSP.Algorithms.Tabu;

/// <summary>
/// Fixed-length FIFO list of forbidden <see cref="TabuMove"/> values.
/// When the list is full, the oldest entry is evicted before adding the new one.
/// </summary>
/// <remarks>
/// Implemented as a <see cref="Queue{T}"/> so enqueue is O(1) amortised and
/// eviction is O(1). Membership check is O(<see cref="MaxLength"/>) but
/// <see cref="MaxLength"/> is bounded by <c>AlgorithmConfig.TabuListLength</c>
/// (typically ≤ 20), so this is effectively O(1) in practice.
/// </remarks>
internal sealed class TabuList
{
    private readonly Queue<TabuMove> _queue;

    /// <summary>Gets the maximum number of entries the list may hold.</summary>
    public int MaxLength { get; }

    /// <summary>Gets the current number of entries in the list.</summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Initialises a new tabu list with the specified maximum length.
    /// </summary>
    /// <param name="maxLength">
    /// Maximum number of forbidden moves to retain. When exceeded, the
    /// oldest move is automatically evicted.
    /// </param>
    public TabuList(int maxLength)
    {
        MaxLength = maxLength;
        _queue = new Queue<TabuMove>(maxLength);
    }

    /// <summary>
    /// Adds <paramref name="move"/> to the list, evicting the oldest entry
    /// first if the list is already at capacity.
    /// </summary>
    /// <param name="move">The move to mark as forbidden.</param>
    public void Add(TabuMove move)
    {
        if (_queue.Count >= MaxLength)
            _queue.Dequeue();
        _queue.Enqueue(move);
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="move"/> is currently
    /// in the tabu list and is therefore forbidden.
    /// </summary>
    /// <param name="move">The move to test.</param>
    public bool IsTabu(TabuMove move) => _queue.Contains(move);

    /// <summary>
    /// Removes all entries from the tabu list.
    /// </summary>
    public void Clear() => _queue.Clear();
}
