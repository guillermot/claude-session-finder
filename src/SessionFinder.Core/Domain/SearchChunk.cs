namespace SessionFinder.Core.Domain;

/// <summary>
/// One unit of indexable text extracted from a transcript. The exchange currency between the
/// parser, the index writer and the search reader.
/// </summary>
/// <param name="Kind">What the text is, which drives ranking weight and replacement rules.</param>
/// <param name="Text">
/// The text itself, no longer than the maximum indexable length. A longer block of message text
/// arrives as several chunks of the same kind rather than as one truncated chunk.
/// </param>
/// <param name="Timestamp">When the record carrying the text was written, when it has one.</param>
public sealed record SearchChunk(ChunkKind Kind, string Text, DateTimeOffset? Timestamp);
