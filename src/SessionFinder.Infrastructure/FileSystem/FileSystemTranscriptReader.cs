using System.Buffers;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.FileSystem;

/// <summary>
/// Opens transcripts from the local file system.
/// </summary>
/// <remarks>
/// <para>
/// The share flags are the part that matters: Claude keeps its own handle on a live transcript and
/// may delete it, so denying either write sharing or delete sharing turns an ordinary active
/// session into an access error.
/// </para>
/// <para>
/// The head hash and the length are taken from the same handle the parse then reads, and the last
/// write time is read before any content is, so the watermark stored afterwards describes a file
/// no newer than the bytes that were parsed. A file rewritten in the middle of a read therefore
/// produces a watermark that fails to match on the next pass, which is the safe direction.
/// </para>
/// </remarks>
public sealed class FileSystemTranscriptReader : ITranscriptFileReader
{
    private const int FileBufferSize = 64 * 1024;

    /// <inheritdoc />
    public async Task<OpenTranscript> OpenAsync(
        string filePath,
        long startOffset,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegative(startOffset);

        var lastWriteTimeUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);
        var stream = OpenForSharedRead(filePath);

        try
        {
            var headSha256 = await ComputeHeadHashAsync(stream, cancellationToken).ConfigureAwait(false);
            var length = stream.Length;
            stream.Seek(startOffset, SeekOrigin.Begin);

            return new OpenTranscript(stream, length, lastWriteTimeUtc, headSha256);
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static FileStream OpenForSharedRead(string filePath) =>
        new(
            filePath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = FileBufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });

    /// <summary>
    /// Hashes the leading bytes, which is what tells a file rewritten to the same length from a
    /// file that was only appended to. An empty file has no head and therefore no hash.
    /// </summary>
    private static async Task<string?> ComputeHeadHashAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(FileFingerprint.HeadSampleSize);

        try
        {
            var read = await stream
                .ReadAtLeastAsync(
                    buffer.AsMemory(0, FileFingerprint.HeadSampleSize),
                    FileFingerprint.HeadSampleSize,
                    throwOnEndOfStream: false,
                    cancellationToken)
                .ConfigureAwait(false);

            return read == 0 ? null : FileFingerprint.ComputeHeadHash(buffer.AsSpan(0, read));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
