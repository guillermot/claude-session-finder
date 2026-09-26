namespace SessionFinder.Core.Domain;

/// <summary>
/// What the index already holds about one session, read back so a later pass can decide whether
/// to open the transcript at all and, when it does, continue from what was already known.
/// </summary>
/// <remarks>
/// The three carried values line up exactly with what a resumed parse needs: the fingerprint
/// answers skip-or-read, and the title candidates and folder are the two things a pass over the
/// tail of a file can no longer discover for itself.
/// </remarks>
public sealed record IndexedSession
{
    /// <summary>The session this row describes.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>The transcript path recorded at the last write.</summary>
    public required string FilePath { get; init; }

    /// <summary>The watermark written by the last successful pass.</summary>
    public required FileFingerprint Fingerprint { get; init; }

    /// <summary>The title currently shown for the session.</summary>
    public required SessionTitle Title { get; init; }

    /// <summary>The candidates the stored title was resolved from.</summary>
    public required SessionTitleCandidates TitleCandidates { get; init; }

    /// <summary>The stored working folder, or <see cref="WorkingFolder.Unknown"/>.</summary>
    public required WorkingFolder Folder { get; init; }

    /// <summary>The parse error recorded on the row, when the last pass skipped damaged lines.</summary>
    public string? ParseError { get; init; }
}
