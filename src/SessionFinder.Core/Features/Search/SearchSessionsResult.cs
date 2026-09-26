namespace SessionFinder.Core.Features.Search;

/// <summary>
/// The answer to a <see cref="SearchSessionsQuery"/>.
/// </summary>
public sealed record SearchSessionsResult
{
    /// <summary>An empty answer, for a query that could not be run at all.</summary>
    public static SearchSessionsResult Empty { get; } = new()
    {
        Hits = [],
        IsRecentFallback = true,
        CandidatesConsidered = 0,
    };

    /// <summary>The ranked results, best first.</summary>
    public required IReadOnlyList<SessionHit> Hits { get; init; }

    /// <summary>
    /// Whether the list is the most recent sessions rather than matches. The search box needs to
    /// say so: showing recent work for an empty query is helpful, and pretending it was a match
    /// is not.
    /// </summary>
    public required bool IsRecentFallback { get; init; }

    /// <summary>
    /// How many sessions the index offered before recency re-ranking and the result limit. Equal
    /// to the candidate limit when the query matched at least that many, which is the signal that
    /// the limit itself may be shaping the answer.
    /// </summary>
    public required int CandidatesConsidered { get; init; }
}
