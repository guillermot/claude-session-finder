using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// What one session said inside a window of time, with enough about the session to group it.
/// </summary>
public sealed record SessionActivity
{
    /// <summary>The session the messages belong to.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>The title currently shown for the session.</summary>
    public required SessionTitle Title { get; init; }

    /// <summary>The folder the session ran in, or <see cref="WorkingFolder.Unknown"/>.</summary>
    public required WorkingFolder Folder { get; init; }

    /// <summary>The git branch recorded for the session, when there was one.</summary>
    public string? GitBranch { get; init; }

    /// <summary>
    /// The message chunks written inside the window, oldest first. Every one carries a timestamp. A
    /// message too long for one chunk arrives as several consecutive chunks sharing its timestamp.
    /// </summary>
    public IReadOnlyList<SearchChunk> Chunks { get; init; } = [];
}
