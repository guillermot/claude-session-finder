namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Opens a transcript for reading.
/// </summary>
/// <remarks>
/// Opening is a port rather than a direct file call because the share flags are not negotiable —
/// the process that writes these files keeps its own handle open and may delete them — and
/// because the head hash has to be taken from the same handle that the parse reads, so the two
/// cannot disagree.
/// </remarks>
public interface ITranscriptFileReader
{
    /// <summary>
    /// Opens a transcript, measures it, and positions it for reading.
    /// </summary>
    /// <param name="filePath">Absolute path of the transcript.</param>
    /// <param name="startOffset">Byte offset the parse should begin at; zero for a full pass.</param>
    /// <param name="cancellationToken">Cancels the open and the head read.</param>
    /// <returns>An open transcript positioned at <paramref name="startOffset"/>.</returns>
    Task<OpenTranscript> OpenAsync(string filePath, long startOffset, CancellationToken cancellationToken);
}
