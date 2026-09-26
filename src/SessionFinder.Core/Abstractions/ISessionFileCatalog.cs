using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Finds the transcript files that represent sessions.
/// </summary>
public interface ISessionFileCatalog
{
    /// <summary>
    /// Lists every session transcript currently on disk.
    /// </summary>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>
    /// The transcripts, most recently written first, so the first thing a cold index learns is
    /// what the user was working on last.
    /// </returns>
    Task<IReadOnlyList<SessionFile>> ListSessionFilesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Measures one file and decides whether it is a session transcript at all.
    /// </summary>
    /// <remarks>
    /// A watcher reports paths, not sessions, and the directories it watches also hold subagent
    /// transcripts, tool results and memory files. Asking the catalogue keeps the answer to "is
    /// this a session" in the one place that already knows it.
    /// </remarks>
    /// <param name="filePath">Absolute path of the file that changed.</param>
    /// <param name="cancellationToken">Cancels the measurement.</param>
    /// <returns>
    /// The transcript, or <see langword="null"/> when the path is not a session transcript or no
    /// longer exists.
    /// </returns>
    Task<SessionFile?> DescribeAsync(string filePath, CancellationToken cancellationToken);
}
