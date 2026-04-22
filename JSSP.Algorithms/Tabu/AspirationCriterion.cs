namespace JSSP.Algorithms.Tabu;

/// <summary>
/// Standard aspiration criterion for Tabu search: a tabu move is permitted
/// if it would produce a solution strictly better than the global best found
/// so far, regardless of its tabu status.
/// </summary>
/// <remarks>
/// This prevents the tabu list from blocking the acceptance of a genuinely
/// superior solution discovered during the search. The criterion is always
/// evaluated before the tabu check is applied, and overrides it when satisfied.
/// </remarks>
internal static class AspirationCriterion
{
    /// <summary>
    /// Returns <see langword="true"/> when applying a move that yields
    /// <paramref name="candidateMakespan"/> would improve upon
    /// <paramref name="globalBestMakespan"/>, allowing a tabu move to be accepted.
    /// </summary>
    /// <param name="candidateMakespan">
    /// The makespan that would result from applying the candidate (tabu) move.
    /// </param>
    /// <param name="globalBestMakespan">
    /// The best makespan found across all iterations and repetitions so far.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="candidateMakespan"/> is strictly
    /// less than <paramref name="globalBestMakespan"/>; <see langword="false"/> otherwise.
    /// </returns>
    public static bool IsSatisfied(int candidateMakespan, int globalBestMakespan) =>
        candidateMakespan < globalBestMakespan;
}
