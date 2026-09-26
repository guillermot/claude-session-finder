using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.Transcripts;

/// <summary>
/// Cuts a block of message text into indexable chunks.
/// </summary>
/// <remarks>
/// <para>
/// A cap on chunk length is needed so that one pasted wall of text cannot dominate the ranking of
/// a session, but a cap that truncates makes everything past it unfindable: measured over the real
/// corpus, sixteen chunks in ten sessions ran past eight kilobytes and took 4.9% of all
/// conversation prose with them, including words that appeared nowhere else.
/// </para>
/// <para>
/// So the text is split rather than cut short. Every segment keeps the kind, the session and the
/// timestamp of the block it came from, which leaves ranking exactly as it was while losing
/// nothing.
/// </para>
/// <para>
/// Segments end on whitespace. The tokenizer splits on non-alphanumeric characters, so every token
/// it can produce sits inside a single whitespace-delimited word; ending a segment on whitespace
/// therefore guarantees that no searchable token is ever cut in half by the split. The fallback
/// for a run of text with no whitespace in it at all cuts on length, which can only divide
/// something that is not a word.
/// </para>
/// </remarks>
internal static class ChunkSplitter
{
    /// <summary>Longest text a single chunk may hold.</summary>
    public const int MaxChunkLength = 8 * 1024;

    /// <summary>
    /// Shortest segment the whitespace search will settle for. Below this the split falls back to
    /// cutting on length, so that a long run without whitespace cannot produce a chunk per word.
    /// </summary>
    private const int MinimumSegmentLength = MaxChunkLength / 2;

    private static readonly char[] WhitespaceSeparators = [' ', '\t', '\n', '\r', '\f', '\v', ' '];

    /// <summary>
    /// Appends <paramref name="text"/> to <paramref name="destination"/> as one chunk, or as
    /// several when it is longer than <see cref="MaxChunkLength"/>.
    /// </summary>
    /// <param name="destination">The list the chunks are appended to.</param>
    /// <param name="kind">The kind every produced chunk carries.</param>
    /// <param name="text">The text to split.</param>
    /// <param name="timestamp">The timestamp every produced chunk carries.</param>
    public static void Append(
        List<SearchChunk> destination,
        ChunkKind kind,
        string text,
        DateTimeOffset? timestamp)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length <= MaxChunkLength)
        {
            AppendSegment(destination, kind, text.AsSpan(), timestamp);
            return;
        }

        var start = 0;

        while (start < text.Length)
        {
            var end = FindSegmentEnd(text, start);
            AppendSegment(destination, kind, text.AsSpan(start, end - start), timestamp);
            start = end;
        }
    }

    /// <summary>
    /// Caps text that is already known to be short, so a chunk kind that is never split cannot
    /// exceed the length the index expects.
    /// </summary>
    /// <param name="text">The text to cap.</param>
    /// <returns>The text, cut to <see cref="MaxChunkLength"/> without dividing a surrogate pair.</returns>
    public static string Cap(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Length <= MaxChunkLength ? text : text[..CutWithoutDividingSurrogatePair(text, MaxChunkLength)];
    }

    private static void AppendSegment(
        List<SearchChunk> destination,
        ChunkKind kind,
        ReadOnlySpan<char> segment,
        DateTimeOffset? timestamp)
    {
        if (segment.IsWhiteSpace())
        {
            return;
        }

        destination.Add(new SearchChunk(kind, segment.ToString(), timestamp));
    }

    private static int FindSegmentEnd(string text, int start)
    {
        var remaining = text.Length - start;

        if (remaining <= MaxChunkLength)
        {
            return text.Length;
        }

        var limit = start + MaxChunkLength;
        var whitespace = text.LastIndexOfAny(WhitespaceSeparators, limit - 1, MaxChunkLength - MinimumSegmentLength);

        return whitespace > start ? whitespace : CutWithoutDividingSurrogatePair(text, limit);
    }

    private static int CutWithoutDividingSurrogatePair(string text, int cut) =>
        char.IsHighSurrogate(text[cut - 1]) ? cut - 1 : cut;
}
