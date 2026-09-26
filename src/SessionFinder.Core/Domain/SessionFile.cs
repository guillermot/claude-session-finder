namespace SessionFinder.Core.Domain;

/// <summary>
/// One transcript file as the catalogue found it on disk: enough metadata to decide what to do
/// with it without opening it.
/// </summary>
public sealed record SessionFile
{
    /// <summary>The session the file belongs to, taken from its name.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>Absolute path of the transcript.</summary>
    public required string FilePath { get; init; }

    /// <summary>The file name, which is the title of last resort.</summary>
    public required string FileName { get; init; }

    /// <summary>Length of the file in bytes at the moment it was enumerated.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Last write time in UTC at the moment it was enumerated.</summary>
    public required DateTimeOffset LastWriteTimeUtc { get; init; }

    /// <summary>
    /// Builds the fingerprint that can be measured without reading the file.
    /// </summary>
    /// <returns>
    /// A fingerprint with no head hash and a zero parse offset: both require opening the file, and
    /// the skip decision is reached without either.
    /// </returns>
    public FileFingerprint ToFingerprint() => new(SizeBytes, LastWriteTimeUtc.UtcTicks, HeadSha256: null, ParseOffset: 0);
}
