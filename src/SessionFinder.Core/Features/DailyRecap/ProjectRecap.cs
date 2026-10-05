using SessionFinder.Core.Abstractions;

namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// The work done in one project on one day: its sessions and what was committed.
/// </summary>
/// <remarks>
/// A project is a repository when the folder is inside one, and the folder otherwise. Sessions
/// started in a repository's subfolders, or in a second worktree of it, are the same project to the
/// person describing their day, and they are grouped as one.
/// </remarks>
public sealed record ProjectRecap
{
    /// <summary>The last segment of the project path, which is how people name a project out loud.</summary>
    public required string Name { get; init; }

    /// <summary>The repository root or folder, or an empty string when no folder was recorded.</summary>
    public required string Path { get; init; }

    /// <summary>The branches worked on, in the order they were first seen.</summary>
    public IReadOnlyList<string> Branches { get; init; } = [];

    /// <summary>The sessions, in the order they started.</summary>
    public required IReadOnlyList<SessionRecap> Sessions { get; init; }

    /// <summary>The user's own commits that day, oldest first, with local timestamps.</summary>
    public IReadOnlyList<GitCommit> Commits { get; init; } = [];

    /// <summary>The first message of the day across the project's sessions, in local time.</summary>
    public required DateTimeOffset FirstActivity { get; init; }

    /// <summary>The last message of the day across the project's sessions, in local time.</summary>
    public required DateTimeOffset LastActivity { get; init; }

    /// <summary>How many prompts the project took that day.</summary>
    public int PromptCount => Sessions.Sum(session => session.PromptCount);
}
