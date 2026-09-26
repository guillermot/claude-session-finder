using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.Search;

/// <summary>
/// One ranked result: the session to show, and the three numbers that explain its position.
/// </summary>
/// <remarks>
/// The lexical score and the recency multiplier are carried separately from the final score on
/// purpose. A ranking that cannot be taken apart cannot be tuned, and every kind of tuning starts
/// with "why is that one above this one".
/// </remarks>
public sealed record SessionHit
{
    /// <summary>The session and everything the row displays.</summary>
    public required SessionSummary Session { get; init; }

    /// <summary>What the text alone said, higher being better.</summary>
    public required double LexicalScore { get; init; }

    /// <summary>What recency multiplied it by; exactly one for a session with no known activity.</summary>
    public required double RecencyMultiplier { get; init; }

    /// <summary>The value the result was ordered on.</summary>
    public required double Score { get; init; }

    /// <summary>Which kinds of text matched, ascending.</summary>
    public required IReadOnlyList<ChunkKind> MatchedKinds { get; init; }

    /// <summary>How many individual chunks of the session matched.</summary>
    public required int MatchedChunkCount { get; init; }
}
