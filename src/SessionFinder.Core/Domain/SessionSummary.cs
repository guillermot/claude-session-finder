namespace SessionFinder.Core.Domain;

/// <summary>
/// One row of the result list: title, last activity and folder, plus what an action needs to run.
/// </summary>
public sealed record SessionSummary
{
    /// <summary>The session, which is also the argument a resume needs.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>Absolute path of the transcript file the row came from.</summary>
    public required string FilePath { get; init; }

    /// <summary>The resolved title and the candidate that produced it.</summary>
    public required SessionTitle Title { get; init; }

    /// <summary>The folder to open, or <see cref="WorkingFolder.Unknown"/> when actions must be disabled.</summary>
    public required WorkingFolder Folder { get; init; }

    /// <summary>The git branch recorded for the session, when there was one.</summary>
    public string? GitBranch { get; init; }

    /// <summary>When the session was last written to, which drives the recency component of ranking.</summary>
    public DateTimeOffset? LastActivity { get; init; }

    /// <summary>Human prompts plus assistant turns, so a stub session is recognisable as one.</summary>
    public int MessageCount { get; init; }

    /// <summary>A highlighted fragment of the matching text, when the query produced one.</summary>
    public string? Snippet { get; init; }
}
