using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.Search;

namespace SessionFinder.Core.Tests.Features.Search;

public sealed class SearchSessionsHandlerTests
{
    private const string HyphenatedTicketToken = "ABC-123";
    private const double EqualRelevance = 10.0;

    private static readonly DateTimeOffset Now = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly ISessionIndexReader _reader = Substitute.For<ISessionIndexReader>();
    private readonly SearchOptions _options = new();

    [Fact]
    public async Task HandleAsync_TypedText_SendsItToTheIndexUntouched()
    {
        GivenCandidates();

        await Handler().HandleAsync(SearchSessionsQuery.For(HyphenatedTicketToken), CancellationToken.None);

        await _reader.Received(1).SearchAsync(
            Arg.Is<SessionSearchRequest>(request => request.Text == HyphenatedTicketToken),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmptyText_AsksForRecentSessionsInsteadOfAMatch()
    {
        GivenCandidates();

        await Handler().HandleAsync(SearchSessionsQuery.For(string.Empty), CancellationToken.None);

        await _reader.Received(1).SearchAsync(
            Arg.Is<SessionSearchRequest>(request => request.Text.Length == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmptyText_AsksForNoMoreSessionsThanItWillShow()
    {
        _options.MaxResults = 5;
        GivenCandidates();

        await Handler().HandleAsync(SearchSessionsQuery.For(string.Empty), CancellationToken.None);

        await _reader.Received(1).SearchAsync(
            Arg.Is<SessionSearchRequest>(request => request.CandidateLimit == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_TypedText_AsksForTheWiderCandidateSet()
    {
        _options.MaxResults = 5;
        _options.CandidateLimit = 200;
        GivenCandidates();

        await Handler().HandleAsync(SearchSessionsQuery.For(HyphenatedTicketToken), CancellationToken.None);

        await _reader.Received(1).SearchAsync(
            Arg.Is<SessionSearchRequest>(request => request.CandidateLimit == 200),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EmptyText_ReportsTheResultAsARecentListing()
    {
        GivenCandidates(Candidate("only", EqualRelevance, Now));

        var result = await Handler().HandleAsync(SearchSessionsQuery.For("   "), CancellationToken.None);

        result.IsRecentFallback.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_SingleCharacterText_IsTooShortToMatchOn()
    {
        GivenCandidates();

        var result = await Handler().HandleAsync(SearchSessionsQuery.For("a"), CancellationToken.None);

        result.IsRecentFallback.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_TwoCharacterText_RunsAMatch()
    {
        GivenCandidates();

        var result = await Handler().HandleAsync(SearchSessionsQuery.For("ab"), CancellationToken.None);

        result.IsRecentFallback.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_RecentListing_KeepsTheOrderTheIndexReturned()
    {
        GivenCandidates(
            Candidate("newest", 0, Now),
            Candidate("older", 0, Now.AddDays(-40)));

        var result = await Handler().HandleAsync(SearchSessionsQuery.For(null), CancellationToken.None);

        result.Hits.Select(hit => hit.Session.Title.Text).Should().ContainInOrder("newest", "older");
    }

    [Fact]
    public async Task HandleAsync_EquallyRelevantSessions_RanksTheRecentOneFirst()
    {
        GivenCandidates(
            Candidate("older", EqualRelevance, Now.AddDays(-120)),
            Candidate("newer", EqualRelevance, Now.AddDays(-1)));

        var result = await Handler().HandleAsync(SearchSessionsQuery.For(HyphenatedTicketToken), CancellationToken.None);

        result.Hits[0].Session.Title.Text.Should().Be("newer");
    }

    [Fact]
    public async Task HandleAsync_MuchStrongerOldMatch_StillOutranksAWeakRecentOne()
    {
        GivenCandidates(
            Candidate("weak but recent", EqualRelevance, Now),
            Candidate("strong but old", EqualRelevance * 3, Now.AddDays(-365)));

        var result = await Handler().HandleAsync(SearchSessionsQuery.For(HyphenatedTicketToken), CancellationToken.None);

        result.Hits[0].Session.Title.Text.Should().Be("strong but old");
    }

    [Fact]
    public async Task HandleAsync_RankedHit_ReportsTheLexicalScoreAndTheMultiplierSeparately()
    {
        GivenCandidates(Candidate("only", EqualRelevance, Now));

        var result = await Handler().HandleAsync(SearchSessionsQuery.For(HyphenatedTicketToken), CancellationToken.None);

        var hit = result.Hits[0];
        hit.LexicalScore.Should().Be(EqualRelevance);
        hit.Score.Should().BeApproximately(EqualRelevance * hit.RecencyMultiplier, 1e-9);
    }

    [Fact]
    public async Task HandleAsync_SessionWithoutActivity_EarnsNoRecencyBonus()
    {
        GivenCandidates(Candidate("undated", EqualRelevance, lastActivity: null));

        var result = await Handler().HandleAsync(SearchSessionsQuery.For(HyphenatedTicketToken), CancellationToken.None);

        result.Hits[0].RecencyMultiplier.Should().Be(RecencyBoost.NoBoost);
    }

    [Fact]
    public async Task HandleAsync_MoreCandidatesThanTheLimit_ReturnsOnlyTheLimit()
    {
        _options.MaxResults = 2;
        GivenCandidates(
            Candidate("first", 30, Now),
            Candidate("second", 20, Now),
            Candidate("third", 10, Now));

        var result = await Handler().HandleAsync(SearchSessionsQuery.For(HyphenatedTicketToken), CancellationToken.None);

        result.Hits.Should().HaveCount(2);
        result.CandidatesConsidered.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_QueryAskingForFewerResults_OverridesTheConfiguredLimit()
    {
        GivenCandidates(
            Candidate("first", 30, Now),
            Candidate("second", 20, Now));

        var query = new SearchSessionsQuery { Text = HyphenatedTicketToken, MaxResults = 1 };
        var result = await Handler().HandleAsync(query, CancellationToken.None);

        result.Hits.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_ConfiguredWeights_AreSentToTheIndexWithTheRequest()
    {
        _options.TitleWeight = 42.0;
        GivenCandidates();

        await Handler().HandleAsync(SearchSessionsQuery.For(HyphenatedTicketToken), CancellationToken.None);

        await _reader.Received(1).SearchAsync(
            Arg.Is<SessionSearchRequest>(request => request.Weights.Title == 42.0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ConfiguredRepeatMatchWeight_IsSentToTheIndexWithTheRequest()
    {
        _options.RepeatMatchWeight = 0.1;
        GivenCandidates();

        await Handler().HandleAsync(SearchSessionsQuery.For(HyphenatedTicketToken), CancellationToken.None);

        await _reader.Received(1).SearchAsync(
            Arg.Is<SessionSearchRequest>(request => request.RepeatMatchWeight == 0.1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RecencyTurnedOff_RanksOnRelevanceAlone()
    {
        _options.RecencyWeight = 0;
        GivenCandidates(
            Candidate("older but stronger", EqualRelevance * 2, Now.AddDays(-365)),
            Candidate("newer but weaker", EqualRelevance, Now));

        var result = await Handler().HandleAsync(SearchSessionsQuery.For(HyphenatedTicketToken), CancellationToken.None);

        result.Hits[0].Session.Title.Text.Should().Be("older but stronger");
    }

    private SearchSessionsHandler Handler() => new(
        _reader,
        new FixedOptionsMonitor<SearchOptions>(_options),
        new FakeTimeProvider(Now));

    private void GivenCandidates(params SessionMatch[] candidates) =>
        _reader.SearchAsync(Arg.Any<SessionSearchRequest>(), Arg.Any<CancellationToken>())
            .Returns(candidates);

    private static SessionMatch Candidate(string title, double lexicalScore, DateTimeOffset? lastActivity) => new()
    {
        Session = new SessionSummary
        {
            SessionId = new SessionId(Guid.NewGuid()),
            FilePath = $@"C:\projects\encoded\{title}.jsonl",
            Title = new SessionTitle(title, TitleSource.AiTitle),
            Folder = WorkingFolder.FromTranscriptCwd(@"C:\git\Example"),
            LastActivity = lastActivity,
        },
        LexicalScore = lexicalScore,
        MatchedKinds = [ChunkKind.Title],
        MatchedChunkCount = 1,
    };
}
