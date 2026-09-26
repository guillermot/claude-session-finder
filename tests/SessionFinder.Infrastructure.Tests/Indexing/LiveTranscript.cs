using System.Text;
using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.Tests.Indexing;

/// <summary>
/// One transcript file in the throwaway projects tree, mutated the way Claude mutates a real one.
/// </summary>
/// <remarks>
/// The writes go through the same share flags the indexer reads with, and every method reports the
/// bytes it added, because the promise the incremental pass makes is stated in bytes: the watermark
/// moves by exactly what was appended and by nothing else.
/// </remarks>
internal sealed class LiveTranscript
{
    private const char LineSeparator = '\n';

    private static readonly Encoding TranscriptEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Binds to a path inside a project directory.
    /// </summary>
    /// <param name="filePath">Absolute path of the transcript.</param>
    public LiveTranscript(string filePath)
    {
        FilePath = filePath;
        SessionId.TryParseFromFileName(filePath, out var sessionId);
        SessionId = sessionId;
    }

    /// <summary>Absolute path of the transcript.</summary>
    public string FilePath { get; }

    /// <summary>The session the file name declares.</summary>
    public SessionId SessionId { get; }

    /// <summary>Current length of the file in bytes.</summary>
    public long SizeBytes => new FileInfo(FilePath).Length;

    /// <summary>
    /// Appends complete, newline-terminated records.
    /// </summary>
    /// <param name="lines">The records to append.</param>
    /// <returns>How many bytes were added.</returns>
    public long Append(params string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var payload = string.Concat(lines.Select(line => line + LineSeparator));

        return Write(payload, append: true);
    }

    /// <summary>
    /// Appends a record without its terminating newline, the way a transcript looks while Claude is
    /// still writing it.
    /// </summary>
    /// <param name="line">The record to append.</param>
    /// <returns>How many bytes were added.</returns>
    public long AppendUnterminated(string line) => Write(line, append: true);

    /// <summary>
    /// Terminates the record left half-written by <see cref="AppendUnterminated"/>.
    /// </summary>
    /// <returns>How many bytes were added.</returns>
    public long TerminateLastLine() => Write(LineSeparator.ToString(), append: true);

    /// <summary>
    /// Replaces the whole file, the way a transcript that was rewritten rather than appended to
    /// appears on disk.
    /// </summary>
    /// <param name="lines">The records the file holds afterwards.</param>
    /// <returns>How many bytes the file holds afterwards.</returns>
    public long Rewrite(params string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var payload = string.Concat(lines.Select(line => line + LineSeparator));

        return Write(payload, append: false);
    }

    /// <summary>Removes the transcript from disk.</summary>
    public void Delete() => File.Delete(FilePath);

    private long Write(string payload, bool append)
    {
        var bytes = TranscriptEncoding.GetBytes(payload);

        using (var stream = new FileStream(
            FilePath,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete))
        {
            stream.Write(bytes, 0, bytes.Length);
        }

        return bytes.Length;
    }
}
