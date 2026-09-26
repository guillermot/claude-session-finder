using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// One unit of work for <see cref="ISessionTranscriptParser"/>.
/// </summary>
/// <remarks>
/// The request carries an open <see cref="Stream"/> rather than a path. Opening the file is the
/// caller's problem — it needs share flags that let Claude keep writing — and a stream is what
/// lets the parser be tested from fixtures and in memory on any operating system.
/// </remarks>
public sealed record TranscriptParseRequest
{
    /// <summary>The session being parsed.</summary>
    public required SessionId SessionId { get; init; }

    /// <summary>The transcript file name, used as the title of last resort.</summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The transcript bytes, already positioned at <see cref="StartOffset"/>. The parser reads to
    /// the end and never seeks.
    /// </summary>
    public required Stream Content { get; init; }

    /// <summary>
    /// The absolute byte offset the stream is positioned at, so the returned offset is absolute
    /// too. Zero for a full pass.
    /// </summary>
    public long StartOffset { get; init; }

    /// <summary>
    /// What an earlier pass already knew about the title, so a resumed pass that sees no title
    /// record does not lose the title it had.
    /// </summary>
    public SessionTitleCandidates? KnownTitles { get; init; }

    /// <summary>
    /// The folder an earlier pass recovered. A resumed pass starts after the record that carries
    /// the authoritative <c>cwd</c>, so without this it would report the folder as unknown.
    /// </summary>
    public WorkingFolder? KnownFolder { get; init; }
}
