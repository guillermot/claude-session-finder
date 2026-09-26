using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// A newly resolved title for a session that is already indexed.
/// </summary>
/// <remarks>
/// Title records are appended to a transcript long after the messages they describe, and a session
/// can be renamed repeatedly, so a pass that reads only the new tail regularly finds a better
/// title and nothing else. Rewriting the whole session for that would mean re-reading the file.
/// </remarks>
public sealed record SessionTitleRevision
{
    /// <summary>The session being renamed.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>The newly resolved title.</summary>
    public required SessionTitle Title { get; init; }

    /// <summary>The merged candidate set the new title was resolved from.</summary>
    public required SessionTitleCandidates Candidates { get; init; }

    /// <summary>The watermark reached by the pass that found the new title.</summary>
    public required FileFingerprint Fingerprint { get; init; }

    /// <summary>
    /// The latest moment the pass saw in the tail it read, when it saw one.
    /// </summary>
    /// <remarks>
    /// A tail can carry no message and still prove the session was worked in moments ago: renaming
    /// a session and running a tool both leave records that are not conversation. Carrying the
    /// moment here is what stops a title-only pass from leaving the session looking stale.
    /// </remarks>
    public DateTimeOffset? LastActivity { get; init; }

    /// <summary>When the pass completed.</summary>
    public required DateTimeOffset IndexedAt { get; init; }
}
