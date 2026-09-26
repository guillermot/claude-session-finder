using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// One lookup against the index: what the user typed, how much of the corpus to weigh, and how to
/// weigh it.
/// </summary>
/// <remarks>
/// The text is raw user input. Escaping it belongs to whatever query dialect the adapter speaks,
/// which is the one part of searching that is genuinely specific to the storage engine.
/// </remarks>
public sealed record SessionSearchRequest
{
    /// <summary>
    /// What the user typed. An empty value asks for the most recently active sessions instead of
    /// a match, which is the right answer for an empty search box.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// How many sessions to return before the caller re-ranks them. Higher than the number shown,
    /// because re-ranking can only reorder what it was given.
    /// </summary>
    public required int CandidateLimit { get; init; }

    /// <summary>How much a match counts for, depending on what kind of text it was found in.</summary>
    public required ChunkWeights Weights { get; init; }

    /// <summary>
    /// How much each match after a session's strongest one adds. Below one, so a long
    /// conversation cannot outrank a precise short one on sheer number of mentions.
    /// </summary>
    public required double RepeatMatchWeight { get; init; }

    /// <summary>
    /// How much a chunk is promoted for each typed term beyond the first that it matched on its
    /// own. This is what keeps words found close together ranked above the same words found far
    /// apart, now that being far apart no longer excludes a session.
    /// </summary>
    public required double CoOccurrenceWeight { get; init; }
}
