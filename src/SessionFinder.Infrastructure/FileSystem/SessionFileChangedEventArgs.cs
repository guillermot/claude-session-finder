namespace SessionFinder.Infrastructure.FileSystem;

/// <summary>
/// Reports that a transcript file may have changed.
/// </summary>
/// <remarks>
/// The path is all a watcher can honestly say. Whether it is a session transcript at all, and what
/// changed in it, is settled later by the catalogue and the parse plan, from the file itself rather
/// than from a notification that may already be out of date.
/// </remarks>
/// <param name="filePath">Absolute path of the file the operating system reported.</param>
public sealed class SessionFileChangedEventArgs(string filePath) : EventArgs
{
    /// <summary>Absolute path of the file the operating system reported.</summary>
    public string FilePath { get; } = filePath;
}
