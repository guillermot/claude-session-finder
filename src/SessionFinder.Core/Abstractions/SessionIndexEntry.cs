using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// One unit of work for <see cref="ISessionIndexWriter.WriteAsync"/>: everything a pass produced,
/// plus where it came from and when it was written.
/// </summary>
public sealed record SessionIndexEntry
{
    /// <summary>What the pass learned from the transcript.</summary>
    public required SessionDocument Document { get; init; }

    /// <summary>Absolute path of the transcript the document came from.</summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The watermark to store. Its parse offset must be the offset the document reports, because
    /// that is the byte the next pass will resume from.
    /// </summary>
    public required FileFingerprint Fingerprint { get; init; }

    /// <summary>When the pass completed.</summary>
    public required DateTimeOffset IndexedAt { get; init; }
}
