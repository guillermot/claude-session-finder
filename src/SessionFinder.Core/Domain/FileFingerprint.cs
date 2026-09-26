using System.Security.Cryptography;

namespace SessionFinder.Core.Domain;

/// <summary>
/// The watermark of a transcript file: enough to decide whether it can be skipped, resumed from,
/// or has to be read again from the start.
/// </summary>
/// <param name="Size">Length of the file in bytes.</param>
/// <param name="MTimeTicks">Last write time in UTC ticks.</param>
/// <param name="HeadSha256">
/// Hash of the first <see cref="HeadSampleSize"/> bytes, which detects a file rewritten to the
/// same length. <see langword="null"/> when it was never computed.
/// </param>
/// <param name="ParseOffset">Byte offset the last successful pass stopped at.</param>
public readonly record struct FileFingerprint(long Size, long MTimeTicks, string? HeadSha256, long ParseOffset)
{
    /// <summary>Number of leading bytes covered by <see cref="HeadSha256"/>.</summary>
    public const int HeadSampleSize = 4096;

    /// <summary>
    /// Hashes the leading bytes of a transcript file.
    /// </summary>
    /// <param name="head">At most <see cref="HeadSampleSize"/> bytes read from offset zero.</param>
    /// <returns>An uppercase hexadecimal digest.</returns>
    public static string ComputeHeadHash(ReadOnlySpan<byte> head) => Convert.ToHexString(SHA256.HashData(head));
}
