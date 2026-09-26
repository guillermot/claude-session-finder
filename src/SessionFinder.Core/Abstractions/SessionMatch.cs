using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// One session the index matched, with everything the row needs to be shown and everything the
/// ranking policy needs to place it.
/// </summary>
/// <remarks>
/// The score here is purely lexical: it is what the text said, with no opinion about when the
/// session happened. Time is applied by the search slice, so the recency policy can be retuned
/// without touching a query.
/// </remarks>
public sealed record SessionMatch
{
    /// <summary>Everything the result row displays.</summary>
    public required SessionSummary Session { get; init; }

    /// <summary>
    /// The weighted lexical relevance, higher being better. Zero when the request asked for recent
    /// sessions rather than for a match.
    /// </summary>
    public required double LexicalScore { get; init; }

    /// <summary>Which kinds of text matched, ascending. Empty when nothing was matched.</summary>
    public required IReadOnlyList<ChunkKind> MatchedKinds { get; init; }

    /// <summary>How many individual chunks of the session matched.</summary>
    public required int MatchedChunkCount { get; init; }
}
