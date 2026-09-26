namespace SessionFinder.Core.Features.IndexSessionFile;

/// <summary>
/// Brings one transcript into the index, deciding for itself whether to skip it, resume it or read
/// it again from the start.
/// </summary>
public interface IIndexSessionFileHandler
{
    /// <summary>
    /// Indexes one transcript.
    /// </summary>
    /// <param name="command">The transcript and how insistently to read it.</param>
    /// <param name="cancellationToken">Cancels the parse; a write in progress is left to finish.</param>
    /// <returns>What was done and what it cost.</returns>
    Task<IndexSessionFileResult> HandleAsync(IndexSessionFileCommand command, CancellationToken cancellationToken);
}
