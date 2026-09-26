using System.Text.Json;

namespace SessionFinder.Infrastructure.Transcripts;

/// <summary>
/// Pulls the human-readable text out of a transcript <c>message</c> object.
/// </summary>
/// <remarks>
/// <para>
/// Two shapes exist on disk and both carry real prompts: <c>content</c> is normally an array of
/// typed blocks, but in a measured sample three of 185 user records had it as a plain string. A
/// reader that handles only the array shape drops those prompts without failing, which is the
/// worst possible outcome for a search index.
/// </para>
/// <para>
/// From the array shape only <c>text</c> blocks are taken. <c>thinking</c>, <c>tool_use</c>,
/// <c>tool_result</c> and <c>image</c> blocks are not what anyone searches their history for, and
/// they are where the bytes are.
/// </para>
/// </remarks>
public static class MessageContentReader
{
    /// <summary>
    /// Appends every text block of a message to <paramref name="destination"/>.
    /// </summary>
    /// <param name="messageJson">The bytes of a single <c>message</c> object.</param>
    /// <param name="destination">Receives one entry per text block, in order.</param>
    /// <returns><see langword="false"/> when the message is not well-formed JSON.</returns>
    public static bool TryReadTextBlocks(ReadOnlySpan<byte> messageJson, ICollection<string> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (messageJson.IsEmpty)
        {
            return false;
        }

        try
        {
            ReadTextBlocks(messageJson, destination);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ReadTextBlocks(ReadOnlySpan<byte> messageJson, ICollection<string> destination)
    {
        var reader = new Utf8JsonReader(messageJson, isFinalBlock: true, state: default);

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            return;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals(ContentProperty))
            {
                ReadContentValue(ref reader, destination);
                return;
            }

            reader.Read();
            reader.Skip();
        }
    }

    private static void ReadContentValue(ref Utf8JsonReader reader, ICollection<string> destination)
    {
        reader.Read();

        if (reader.TokenType == JsonTokenType.String)
        {
            AppendIfNotBlank(reader.GetString(), destination);
            return;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                ReadBlock(ref reader, destination);
                continue;
            }

            reader.Skip();
        }
    }

    private static void ReadBlock(ref Utf8JsonReader reader, ICollection<string> destination)
    {
        string? text = null;
        var isTextBlock = false;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (reader.ValueTextEquals(TypeProperty))
            {
                reader.Read();
                isTextBlock = reader.ValueTextEquals(TextBlockType);

                if (!isTextBlock)
                {
                    SkipRemainingProperties(ref reader);
                    return;
                }

                continue;
            }

            text = ReadBlockProperty(ref reader) ?? text;
        }

        if (isTextBlock)
        {
            AppendIfNotBlank(text, destination);
        }
    }

    /// <summary>
    /// Reads one property of a block, returning its value only when the property is the text
    /// payload. Everything else is skipped without allocating.
    /// </summary>
    private static string? ReadBlockProperty(ref Utf8JsonReader reader)
    {
        var isTextPayload = reader.ValueTextEquals(TextProperty);

        reader.Read();

        if (!isTextPayload || reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return null;
        }

        return reader.GetString();
    }

    /// <summary>
    /// Leaves the current object once its <c>type</c> has ruled it out, without reading any of its
    /// remaining values.
    /// </summary>
    private static void SkipRemainingProperties(ref Utf8JsonReader reader)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            reader.Read();
            reader.Skip();
        }
    }

    private static void AppendIfNotBlank(string? text, ICollection<string> destination)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            destination.Add(text);
        }
    }

    private static ReadOnlySpan<byte> ContentProperty => "content"u8;

    private static ReadOnlySpan<byte> TypeProperty => "type"u8;

    private static ReadOnlySpan<byte> TextProperty => "text"u8;

    private static ReadOnlySpan<byte> TextBlockType => "text"u8;
}
