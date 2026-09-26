namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Which piece of a session a copy puts on the clipboard.
/// </summary>
public enum SessionDetail
{
    /// <summary>The folder the session was started in, as a path.</summary>
    FolderPath = 0,

    /// <summary>The session identifier on its own.</summary>
    SessionId = 1,

    /// <summary>
    /// The whole resume as a pasteable line, including the change of directory that the resume
    /// needs in order to find the session at all.
    /// </summary>
    ResumeCommandLine = 2,
}
