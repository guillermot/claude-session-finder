using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Configuration;

/// <summary>
/// The ranking policy: how many results to return, how far a match is allowed to reach, and how
/// much a recent session is favoured over an older one.
/// </summary>
/// <remarks>
/// Every value here is a tuning knob rather than a constant of the problem, which is why none of
/// them is written into a query or a method body. Retuning is a settings change; it never needs
/// the index rebuilt.
/// </remarks>
public sealed class SearchOptions
{
    /// <summary>Configuration section the options are bound from.</summary>
    public const string SectionName = "Finder:Search";

    /// <summary>How many ranked sessions a search returns unless the caller asks for fewer.</summary>
    public const int DefaultMaxResults = 30;

    /// <summary>How many sessions the index is asked for before recency is applied.</summary>
    public const int DefaultCandidateLimit = 200;

    /// <summary>Shortest input that is treated as a search rather than as "show me recent work".</summary>
    public const int DefaultMinimumQueryLength = 2;

    /// <summary>How much a session written today can outrank an equally relevant old one.</summary>
    public const double DefaultRecencyWeight = 0.6;

    /// <summary>How many days it takes for the recency bonus to decay by a factor of e.</summary>
    public const double DefaultRecencyHalfLifeDays = 30.0;

    /// <summary>How much each match after the strongest one in a session adds to its score.</summary>
    public const double DefaultRepeatMatchWeight = 0.0;

    /// <summary>How much a chunk gains for each typed term beyond the first that it also holds.</summary>
    public const double DefaultCoOccurrenceWeight = 2.0;

    /// <summary>How many ranked sessions a search returns unless the caller asks for fewer.</summary>
    public int MaxResults { get; set; } = DefaultMaxResults;

    /// <summary>
    /// How many sessions the index returns before recency re-ranking. It is deliberately much
    /// larger than <see cref="MaxResults"/>: recency can only promote a session that was fetched,
    /// so the cut-off has to sit well below the point where it could change the visible answer.
    /// </summary>
    public int CandidateLimit { get; set; } = DefaultCandidateLimit;

    /// <summary>
    /// Shortest input that runs a match. A single character behaves as a prefix over the whole
    /// corpus, which is slow and tells the user nothing, so shorter input lists recent sessions.
    /// </summary>
    public int MinimumQueryLength { get; set; } = DefaultMinimumQueryLength;

    /// <summary>
    /// Size of the recency bonus at zero age. A value of <c>0.6</c> means a session touched today
    /// scores 1.6 times its lexical relevance, and one from long ago scores exactly its relevance.
    /// </summary>
    public double RecencyWeight { get; set; } = DefaultRecencyWeight;

    /// <summary>How many days it takes for the recency bonus to decay by a factor of e.</summary>
    public double RecencyHalfLifeDays { get; set; } = DefaultRecencyHalfLifeDays;

    /// <summary>
    /// How much each match after a session's strongest one adds to its score. One counts every
    /// match in full; zero scores a session purely by its single best chunk.
    /// </summary>
    /// <remarks>
    /// The default changed to zero when the conjunction moved from the chunk to the session. While
    /// every matching chunk had to contain every typed word, counting the extra matches measured
    /// harmless. Once a chunk can match a single word, accumulating them rewards how much a
    /// session says rather than how well it answers: swept from zero to one over a set of phrases
    /// whose owner was established from the raw transcripts, every step up demoted the right
    /// session, from a mean rank of 2.75 at zero to 10.50 at one. The knob stays for a corpus
    /// where repetition is the signal.
    /// </remarks>
    public double RepeatMatchWeight { get; set; } = DefaultRepeatMatchWeight;

    /// <summary>
    /// How much a chunk gains for each typed term beyond the first that it also holds. Zero ranks
    /// a chunk holding every typed word the same as one holding a single word.
    /// </summary>
    /// <remarks>
    /// This is the replacement for the adjacency that requiring one chunk used to enforce, and it
    /// is why widening the conjunction did not cost ranking: over the same set of phrases, the
    /// mean rank of the right session went from 6.50 without it to 2.75 at the default, which is
    /// better than requiring one chunk ever managed.
    /// </remarks>
    public double CoOccurrenceWeight { get; set; } = DefaultCoOccurrenceWeight;

    /// <summary>Weight of a match in the resolved session title.</summary>
    public double TitleWeight { get; set; } = ChunkWeights.DefaultTitleWeight;

    /// <summary>Weight of a match in the working folder or git branch.</summary>
    public double FolderWeight { get; set; } = ChunkWeights.DefaultFolderWeight;

    /// <summary>Weight of a match in the prompt shown on the resume banner.</summary>
    public double LastPromptWeight { get; set; } = ChunkWeights.DefaultLastPromptWeight;

    /// <summary>Weight of a match in a human prompt.</summary>
    public double UserPromptWeight { get; set; } = ChunkWeights.DefaultUserPromptWeight;

    /// <summary>Weight of a match in an assistant turn.</summary>
    public double AssistantTextWeight { get; set; } = ChunkWeights.DefaultAssistantTextWeight;

    /// <summary>
    /// Collects the per-kind weights into the value the index reader is handed.
    /// </summary>
    /// <returns>The configured weights.</returns>
    public ChunkWeights ToChunkWeights() => new(
        TitleWeight,
        FolderWeight,
        LastPromptWeight,
        UserPromptWeight,
        AssistantTextWeight);
}
