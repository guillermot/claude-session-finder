using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// A transcript that is open for reading, together with the measurements taken from the same
/// handle so the watermark written afterwards describes the bytes that were actually read.
/// </summary>
public sealed class OpenTranscript : IAsyncDisposable
{
    /// <summary>
    /// Wraps an open stream and its measurements.
    /// </summary>
    /// <param name="content">The stream, already positioned where the parse should start.</param>
    /// <param name="length">Length of the file when it was opened.</param>
    /// <param name="lastWriteTimeUtc">Last write time read before any content was read.</param>
    /// <param name="headSha256">Hash of the leading bytes, or <see langword="null"/> when the file is empty.</param>
    public OpenTranscript(Stream content, long length, DateTimeOffset lastWriteTimeUtc, string? headSha256)
    {
        ArgumentNullException.ThrowIfNull(content);

        Content = content;
        Length = length;
        LastWriteTimeUtc = lastWriteTimeUtc;
        HeadSha256 = headSha256;
    }

    /// <summary>The bytes to parse.</summary>
    public Stream Content { get; }

    /// <summary>Length of the file when it was opened.</summary>
    public long Length { get; }

    /// <summary>Last write time read before any content was read.</summary>
    public DateTimeOffset LastWriteTimeUtc { get; }

    /// <summary>Hash of the leading bytes, or <see langword="null"/> when the file is empty.</summary>
    public string? HeadSha256 { get; }

    /// <summary>
    /// Builds the watermark to store after a pass over this handle.
    /// </summary>
    /// <param name="parseOffset">The offset the pass committed to.</param>
    /// <returns>A fingerprint describing the file as it was read.</returns>
    public FileFingerprint ToFingerprint(long parseOffset) =>
        new(Length, LastWriteTimeUtc.UtcTicks, HeadSha256, parseOffset);

    /// <summary>Closes the underlying stream.</summary>
    /// <returns>A task that completes once the stream is closed.</returns>
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
