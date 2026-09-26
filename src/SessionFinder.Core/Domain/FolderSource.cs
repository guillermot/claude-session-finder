namespace SessionFinder.Core.Domain;

/// <summary>
/// Where the working folder of a session came from. The project directory name under
/// <c>~/.claude/projects</c> is a lossy encoding and is never one of these sources.
/// </summary>
public enum FolderSource
{
    /// <summary>No folder could be recovered; folder-dependent actions must stay disabled.</summary>
    Unknown = 0,

    /// <summary>The <c>cwd</c> of the first conversational record in the transcript.</summary>
    TranscriptCwd = 1,

    /// <summary>The <c>project</c> path recorded in <c>history.jsonl</c>, used only as a fallback.</summary>
    HistoryFile = 2,

    /// <summary>
    /// One of the application's own folders, such as the one the log files are written to. It is
    /// never a session's folder and is never stored against one: it exists so that "show me the
    /// logs" can go through the same tested reveal action as "show me this session's folder"
    /// instead of starting a file manager from a second place.
    /// </summary>
    ApplicationFolder = 3,
}
