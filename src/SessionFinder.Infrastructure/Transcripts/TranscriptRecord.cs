namespace SessionFinder.Infrastructure.Transcripts;

/// <summary>
/// A lightweight view of one transcript line: the handful of fields that matter, plus the bounds
/// of the <c>message</c> object so its text can be extracted later, or not at all.
/// </summary>
/// <remarks>
/// Deferring the message is what keeps a tool-result line cheap. The scanner has to walk past the
/// object to reach the fields after it, but walking tokens costs nothing compared with allocating
/// strings for a 600 KB tool result that the index would then throw away.
/// </remarks>
public readonly record struct TranscriptRecord
{
    /// <summary>The <c>type</c> discriminator.</summary>
    public TranscriptRecordType Type { get; init; }

    /// <summary>Whether the record has a parent. The root of a conversation does not.</summary>
    public bool HasParent { get; init; }

    /// <summary>Whether the record was synthesised rather than typed by the user.</summary>
    public bool IsMeta { get; init; }

    /// <summary>
    /// Whether the record carries a tool result, which makes it the output of a tool call rather
    /// than a prompt, and makes it the bulk of the bytes in a transcript.
    /// </summary>
    public bool HasToolUseResult { get; init; }

    /// <summary>The <c>cwd</c> reported by the record, when it has one.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>The <c>gitBranch</c> reported by the record, when it has one.</summary>
    public string? GitBranch { get; init; }

    /// <summary>The record timestamp, when it has one.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>
    /// The payload of a title-bearing record: <c>customTitle</c>, <c>aiTitle</c> or
    /// <c>lastPrompt</c>, depending on <see cref="Type"/>.
    /// </summary>
    public string? TitleText { get; init; }

    /// <summary>Offset of the <c>message</c> object within the scanned line.</summary>
    public int MessageStart { get; init; }

    /// <summary>Length of the <c>message</c> object, or zero when the record has none.</summary>
    public int MessageLength { get; init; }

    /// <summary>Whether the record carries a <c>message</c> object.</summary>
    public bool HasMessage => MessageLength > 0;
}
