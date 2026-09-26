namespace SessionFinder.Core.Domain;

/// <summary>
/// Everything one pass over a transcript produced: what to show, what to index, and where to
/// resume reading next time.
/// </summary>
public sealed record SessionDocument
{
    /// <summary>The session this document describes.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>The resolved title.</summary>
    public required SessionTitle Title { get; init; }

    /// <summary>
    /// The candidates the title was resolved from, carried so a later pass can merge its own
    /// findings and re-resolve instead of starting over.
    /// </summary>
    public required SessionTitleCandidates TitleCandidates { get; init; }

    /// <summary>The working folder, or <see cref="WorkingFolder.Unknown"/> when unrecoverable.</summary>
    public required WorkingFolder Folder { get; init; }

    /// <summary>The git branch reported alongside the working folder, when there was one.</summary>
    public string? GitBranch { get; init; }

    /// <summary>The prompt the session shows in its resume banner, when the transcript records one.</summary>
    public string? LastPrompt { get; init; }

    /// <summary>Timestamp of the earliest record carrying one.</summary>
    public DateTimeOffset? FirstActivity { get; init; }

    /// <summary>Timestamp of the latest record carrying one.</summary>
    public DateTimeOffset? LastActivity { get; init; }

    /// <summary>Human prompts plus assistant turns. Tool traffic is not counted.</summary>
    public int MessageCount { get; init; }

    /// <summary>The indexable text, in the order it was found.</summary>
    public IReadOnlyList<SearchChunk> Chunks { get; init; } = [];

    /// <summary>
    /// The byte offset a later pass must seek to. It sits immediately after the last newline that
    /// terminated a complete line, so a half-written final line is read again rather than lost.
    /// </summary>
    public required long ParseOffset { get; init; }

    /// <summary>
    /// Set when complete lines failed to parse as JSON. The pass still succeeds: a damaged line is
    /// recorded and skipped rather than failing the whole batch.
    /// </summary>
    public string? ParseError { get; init; }
}
