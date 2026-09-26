using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;

namespace SessionFinder.Core.Features.Search;

/// <summary>
/// Owns the ranking policy: it asks the index for lexically relevant candidates and decides, in
/// one place, how much the age of a session is allowed to move it.
/// </summary>
/// <remarks>
/// <para>
/// The split with the index is deliberate. Text relevance needs the whole corpus and belongs in
/// the query; the recency curve needs one timestamp per row and belongs here, where it is a pure
/// function of values already fetched and can be changed without a migration.
/// </para>
/// <para>
/// Input too short to search on is answered with recent sessions rather than with an error or an
/// empty list. An opened search box with nothing typed in it is the commonest state there is, and
/// a one-character prefix matches so much of the corpus that the answer would be noise.
/// </para>
/// </remarks>
public sealed class SearchSessionsHandler(
    ISessionIndexReader reader,
    IOptionsMonitor<SearchOptions> options,
    TimeProvider timeProvider) : ISearchSessionsHandler
{
    private const int SmallestUsefulLimit = 1;
    private const double NoLexicalScore = 0.0;

    /// <inheritdoc />
    public async Task<SearchSessionsResult> HandleAsync(
        SearchSessionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var settings = options.CurrentValue;
        var text = query.Text?.Trim() ?? string.Empty;
        var isLexicalSearch = text.Length >= settings.MinimumQueryLength;
        var resultLimit = ResolveResultLimit(query, settings);

        var matches = await reader
            .SearchAsync(BuildRequest(text, isLexicalSearch, settings, resultLimit), cancellationToken)
            .ConfigureAwait(false);

        return new SearchSessionsResult
        {
            Hits = Rank(matches, settings, isLexicalSearch, resultLimit),
            IsRecentFallback = !isLexicalSearch,
            CandidatesConsidered = matches.Count,
        };
    }

    /// <summary>
    /// Fetches a wide set of candidates for a real search, because re-ranking can only reorder what
    /// it was given, and exactly as many rows as will be shown when there is nothing to rank: the
    /// index already returns those in the order they will be displayed.
    /// </summary>
    private static SessionSearchRequest BuildRequest(
        string text,
        bool isLexicalSearch,
        SearchOptions settings,
        int resultLimit) => new()
    {
        Text = isLexicalSearch ? text : string.Empty,
        CandidateLimit = isLexicalSearch
            ? Math.Max(SmallestUsefulLimit, settings.CandidateLimit)
            : resultLimit,
        Weights = settings.ToChunkWeights(),
        RepeatMatchWeight = settings.RepeatMatchWeight,
        CoOccurrenceWeight = settings.CoOccurrenceWeight,
    };

    private static int ResolveResultLimit(SearchSessionsQuery query, SearchOptions settings) =>
        Math.Max(SmallestUsefulLimit, query.MaxResults ?? settings.MaxResults);

    private IReadOnlyList<SessionHit> Rank(
        IReadOnlyList<SessionMatch> matches,
        SearchOptions settings,
        bool isLexicalSearch,
        int limit)
    {
        var now = timeProvider.GetUtcNow();
        var hits = matches.Select(match => Score(match, settings, now, isLexicalSearch));

        return isLexicalSearch
            ? [.. hits.OrderByDescending(hit => hit.Score).ThenByDescending(LastActivityOf).Take(limit)]
            : [.. hits.Take(limit)];
    }

    /// <summary>
    /// Scores one candidate. With nothing to match on, relevance is meaningless and the recency
    /// multiplier is promoted to the score itself, which keeps the ordering the index already
    /// produced and keeps the reported number monotonic in how recent the session is.
    /// </summary>
    private static SessionHit Score(
        SessionMatch match,
        SearchOptions settings,
        DateTimeOffset now,
        bool isLexicalSearch)
    {
        var multiplier = RecencyBoost.Multiplier(
            match.Session.LastActivity,
            now,
            settings.RecencyWeight,
            settings.RecencyHalfLifeDays);

        return new SessionHit
        {
            Session = match.Session,
            LexicalScore = isLexicalSearch ? match.LexicalScore : NoLexicalScore,
            RecencyMultiplier = multiplier,
            Score = isLexicalSearch ? match.LexicalScore * multiplier : multiplier,
            MatchedKinds = match.MatchedKinds,
            MatchedChunkCount = match.MatchedChunkCount,
        };
    }

    private static DateTimeOffset LastActivityOf(SessionHit hit) =>
        hit.Session.LastActivity ?? DateTimeOffset.MinValue;
}
