using System.Text.Json;

namespace SessionFinder.Infrastructure.Transcripts;

/// <summary>
/// Reads one transcript line into a <see cref="TranscriptRecord"/> with a forward-only
/// <see cref="Utf8JsonReader"/> over the raw bytes.
/// </summary>
/// <remarks>
/// <para>
/// <c>JsonDocument</c> is deliberately not used: it materialises the whole line, and most lines in
/// a real transcript are tool traffic that is discarded immediately. Two early-outs do the rest of
/// the work. Scanning stops outright as soon as <c>toolUseResult</c> appears, which is the field
/// that marks the largest lines on disk; a <c>type</c> that resolves to a kind nothing is indexed
/// from narrows the scan to the timestamp rather than abandoning the line, because when such a
/// record was written is how the index knows a session is still being worked in.
/// </para>
/// <para>
/// Both early-outs depend on where the fields sit, and both were measured rather than assumed:
/// across the transcripts on this machine <c>timestamp</c> always follows <c>type</c>, and always
/// precedes <c>toolUseResult</c>. So no timestamp is lost by either, and the expensive payload is
/// still never read.
/// </para>
/// </remarks>
public static class TranscriptRecordScanner
{
    /// <summary>
    /// Scans one line.
    /// </summary>
    /// <param name="line">The line bytes, without its newline.</param>
    /// <param name="record">The fields recovered from the line.</param>
    /// <returns>
    /// <see langword="false"/> when the line is not a well-formed JSON object, which for the last
    /// line of a live transcript usually means it is still being written.
    /// </returns>
    public static bool TryScan(ReadOnlySpan<byte> line, out TranscriptRecord record)
    {
        record = default;

        if (line.IsEmpty)
        {
            return false;
        }

        try
        {
            record = Scan(line);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static TranscriptRecord Scan(ReadOnlySpan<byte> line)
    {
        var reader = new Utf8JsonReader(line, isFinalBlock: true, state: default);
        var state = default(ScanState);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A transcript line must be a JSON object.");
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (!ReadProperty(ref reader, ref state))
            {
                break;
            }
        }

        return state.ToRecord();
    }

    /// <summary>
    /// Consumes one property and its value.
    /// </summary>
    /// <returns><see langword="false"/> when nothing later in the line can matter any more.</returns>
    private static bool ReadProperty(ref Utf8JsonReader reader, ref ScanState state)
    {
        if (state.TimestampOnly)
        {
            return ReadTimestampOnly(ref reader, ref state);
        }

        if (reader.ValueTextEquals(Names.Type))
        {
            reader.Read();
            state.Type = MapRecordType(ref reader);
            state.TimestampOnly = IsIgnored(state.Type);
            return true;
        }

        if (reader.ValueTextEquals(Names.ToolUseResult))
        {
            state.HasToolUseResult = true;
            return false;
        }

        if (reader.ValueTextEquals(Names.Message))
        {
            CaptureMessageBounds(ref reader, ref state);
            return true;
        }

        return ReadSecondaryProperty(ref reader, ref state);
    }

    private static bool ReadSecondaryProperty(ref Utf8JsonReader reader, ref ScanState state)
    {
        if (reader.ValueTextEquals(Names.ParentUuid))
        {
            reader.Read();
            state.HasParent = reader.TokenType == JsonTokenType.String;
            reader.Skip();
            return true;
        }

        if (reader.ValueTextEquals(Names.Cwd))
        {
            state.WorkingDirectory = ReadOptionalString(ref reader);
            return true;
        }

        if (reader.ValueTextEquals(Names.Timestamp))
        {
            ReadTimestamp(ref reader, ref state);
            return true;
        }

        return ReadRemainingProperty(ref reader, ref state);
    }

    /// <summary>
    /// Keeps reading a record nothing is indexed from, for the one field that still matters about
    /// it: when it was written.
    /// </summary>
    /// <remarks>
    /// A transcript records far more than its conversation, and the trailing record of a busy
    /// session is routinely one of these rather than a message. Dropping their timestamps made the
    /// index report a session as older than it was, so the early-out narrows to a single field
    /// instead of abandoning the line. Measured over the transcripts on this machine,
    /// <c>timestamp</c> always follows <c>type</c>, so this reads a property or two rather than the
    /// rest of the record.
    /// </remarks>
    /// <returns><see langword="false"/> once the timestamp has been found.</returns>
    private static bool ReadTimestampOnly(ref Utf8JsonReader reader, ref ScanState state)
    {
        if (!reader.ValueTextEquals(Names.Timestamp))
        {
            reader.Read();
            reader.Skip();
            return true;
        }

        ReadTimestamp(ref reader, ref state);
        return false;
    }

    private static void ReadTimestamp(ref Utf8JsonReader reader, ref ScanState state)
    {
        reader.Read();
        state.Timestamp = reader.TokenType == JsonTokenType.String && reader.TryGetDateTimeOffset(out var value)
            ? value
            : null;
        reader.Skip();
    }

    private static bool ReadRemainingProperty(ref Utf8JsonReader reader, ref ScanState state)
    {
        if (reader.ValueTextEquals(Names.GitBranch))
        {
            state.GitBranch = ReadOptionalString(ref reader);
            return true;
        }

        if (reader.ValueTextEquals(Names.IsMeta))
        {
            reader.Read();
            state.IsMeta = reader.TokenType == JsonTokenType.True;
            reader.Skip();
            return true;
        }

        if (IsTitlePayload(ref reader))
        {
            state.TitleText = ReadOptionalString(ref reader);
            return true;
        }

        reader.Read();
        reader.Skip();
        return true;
    }

    private static bool IsTitlePayload(ref Utf8JsonReader reader) =>
        reader.ValueTextEquals(Names.CustomTitle)
        || reader.ValueTextEquals(Names.AiTitle)
        || reader.ValueTextEquals(Names.LastPrompt);

    /// <summary>
    /// Records where the <c>message</c> object starts and ends instead of reading it, so the
    /// caller can decide whether its text is worth materialising.
    /// </summary>
    private static void CaptureMessageBounds(ref Utf8JsonReader reader, ref ScanState state)
    {
        reader.Read();

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return;
        }

        var start = (int)reader.TokenStartIndex;
        reader.Skip();

        state.MessageStart = start;
        state.MessageLength = (int)reader.BytesConsumed - start;
    }

    private static string? ReadOptionalString(ref Utf8JsonReader reader)
    {
        reader.Read();

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        reader.Skip();
        return null;
    }

    private static TranscriptRecordType MapRecordType(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            return TranscriptRecordType.Unknown;
        }

        var conversation = MapConversationType(ref reader);

        return conversation == TranscriptRecordType.Unknown ? MapSideChannelType(ref reader) : conversation;
    }

    private static TranscriptRecordType MapConversationType(ref Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals(Types.User))
        {
            return TranscriptRecordType.User;
        }

        if (reader.ValueTextEquals(Types.Assistant))
        {
            return TranscriptRecordType.Assistant;
        }

        if (reader.ValueTextEquals(Types.CustomTitle))
        {
            return TranscriptRecordType.CustomTitle;
        }

        if (reader.ValueTextEquals(Types.AiTitle))
        {
            return TranscriptRecordType.AiTitle;
        }

        return reader.ValueTextEquals(Types.LastPrompt) ? TranscriptRecordType.LastPrompt : TranscriptRecordType.Unknown;
    }

    private static TranscriptRecordType MapSideChannelType(ref Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals(Types.Attachment))
        {
            return TranscriptRecordType.Attachment;
        }

        if (reader.ValueTextEquals(Types.System))
        {
            return TranscriptRecordType.System;
        }

        if (reader.ValueTextEquals(Types.Mode))
        {
            return TranscriptRecordType.Mode;
        }

        if (reader.ValueTextEquals(Types.QueueOperation))
        {
            return TranscriptRecordType.QueueOperation;
        }

        if (reader.ValueTextEquals(Types.AtisLatch))
        {
            return TranscriptRecordType.AtisLatch;
        }

        return MapFileHistoryType(ref reader);
    }

    private static TranscriptRecordType MapFileHistoryType(ref Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals(Types.FileHistorySnapshot))
        {
            return TranscriptRecordType.FileHistorySnapshot;
        }

        if (reader.ValueTextEquals(Types.FileHistoryDelta))
        {
            return TranscriptRecordType.FileHistoryDelta;
        }

        if (reader.ValueTextEquals(Types.BridgeSession))
        {
            return TranscriptRecordType.BridgeSession;
        }

        if (reader.ValueTextEquals(Types.PrLink))
        {
            return TranscriptRecordType.PrLink;
        }

        return reader.ValueTextEquals(Types.FrameLink) ? TranscriptRecordType.FrameLink : TranscriptRecordType.Unknown;
    }

    /// <summary>
    /// Whether nothing is ever indexed from this kind of record. An unknown kind is not ignored:
    /// a future record type may still be the only one carrying a working directory.
    /// </summary>
    private static bool IsIgnored(TranscriptRecordType type) => type
        is TranscriptRecordType.Attachment
        or TranscriptRecordType.System
        or TranscriptRecordType.Mode
        or TranscriptRecordType.QueueOperation
        or TranscriptRecordType.AtisLatch
        or TranscriptRecordType.BridgeSession
        or TranscriptRecordType.PrLink
        or TranscriptRecordType.FrameLink
        or TranscriptRecordType.FileHistorySnapshot
        or TranscriptRecordType.FileHistoryDelta;

    private struct ScanState
    {
        public TranscriptRecordType Type;
        public bool HasParent;
        public bool IsMeta;
        public bool HasToolUseResult;
        public bool TimestampOnly;
        public string? WorkingDirectory;
        public string? GitBranch;
        public DateTimeOffset? Timestamp;
        public string? TitleText;
        public int MessageStart;
        public int MessageLength;

        public readonly TranscriptRecord ToRecord() => new()
        {
            Type = Type,
            HasParent = HasParent,
            IsMeta = IsMeta,
            HasToolUseResult = HasToolUseResult,
            WorkingDirectory = WorkingDirectory,
            GitBranch = GitBranch,
            Timestamp = Timestamp,
            TitleText = TitleText,
            MessageStart = MessageStart,
            MessageLength = MessageLength,
        };
    }

    private static class Names
    {
        public static ReadOnlySpan<byte> Type => "type"u8;
        public static ReadOnlySpan<byte> Message => "message"u8;
        public static ReadOnlySpan<byte> ToolUseResult => "toolUseResult"u8;
        public static ReadOnlySpan<byte> ParentUuid => "parentUuid"u8;
        public static ReadOnlySpan<byte> Cwd => "cwd"u8;
        public static ReadOnlySpan<byte> GitBranch => "gitBranch"u8;
        public static ReadOnlySpan<byte> Timestamp => "timestamp"u8;
        public static ReadOnlySpan<byte> IsMeta => "isMeta"u8;
        public static ReadOnlySpan<byte> CustomTitle => "customTitle"u8;
        public static ReadOnlySpan<byte> AiTitle => "aiTitle"u8;
        public static ReadOnlySpan<byte> LastPrompt => "lastPrompt"u8;
    }

    private static class Types
    {
        public static ReadOnlySpan<byte> User => "user"u8;
        public static ReadOnlySpan<byte> Assistant => "assistant"u8;
        public static ReadOnlySpan<byte> CustomTitle => "custom-title"u8;
        public static ReadOnlySpan<byte> AiTitle => "ai-title"u8;
        public static ReadOnlySpan<byte> LastPrompt => "last-prompt"u8;
        public static ReadOnlySpan<byte> Attachment => "attachment"u8;
        public static ReadOnlySpan<byte> System => "system"u8;
        public static ReadOnlySpan<byte> Mode => "mode"u8;
        public static ReadOnlySpan<byte> QueueOperation => "queue-operation"u8;
        public static ReadOnlySpan<byte> AtisLatch => "atis-latch"u8;
        public static ReadOnlySpan<byte> BridgeSession => "bridge-session"u8;
        public static ReadOnlySpan<byte> PrLink => "pr-link"u8;
        public static ReadOnlySpan<byte> FrameLink => "frame-link"u8;
        public static ReadOnlySpan<byte> FileHistorySnapshot => "file-history-snapshot"u8;
        public static ReadOnlySpan<byte> FileHistoryDelta => "file-history-delta"u8;
    }
}
