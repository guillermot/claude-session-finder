using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Infrastructure.Persistence;

namespace SessionFinder.Infrastructure.Tests.Persistence;

public sealed class SqliteSessionIndexSearchTests : IDisposable
{
    private const string HyphenatedTicketToken = "ABC-123";
    private const int CandidateLimit = 200;

    private static readonly DateTimeOffset Noon = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly TemporaryIndex _index = new();

    [Fact]
    public async Task SearchAsync_HyphenatedTicketToken_FindsTheSessionThatMentionsIt()
    {
        await WriteSessionAsync("the wanted one", chunks: [Prompt($"the failure reported in {HyphenatedTicketToken}")]);
        await WriteSessionAsync("something else", chunks: [Prompt("an unrelated conversation")]);

        var matches = await SearchAsync(HyphenatedTicketToken);

        matches.Should().ContainSingle();
        matches[0].Session.Title.Text.Should().Be("the wanted one");
    }

    [Fact]
    public async Task SearchAsync_HyphenatedTicketToken_DoesNotMatchItsPartsFoundApart()
    {
        await WriteSessionAsync("parts apart", chunks: [Prompt("abc was mentioned and 123 much later")]);

        var matches = await SearchAsync(HyphenatedTicketToken);

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_TwoWordsFoundInDifferentMessages_StillFindsTheSession()
    {
        await WriteSessionAsync(
            "discussed across turns",
            chunks: [Prompt("the deposit never arrived"), AssistantText("the transfer was rejected by the bank")]);

        var matches = await SearchAsync("deposit rejected");

        matches.Should().ContainSingle();
        matches[0].Session.Title.Text.Should().Be("discussed across turns");
    }

    [Fact]
    public async Task SearchAsync_TwoWords_ExcludesASessionHoldingOnlyOneOfThem()
    {
        await WriteSessionAsync("holds both", chunks: [Prompt("the deposit was rejected")]);
        await WriteSessionAsync("holds one", chunks: [Prompt("the deposit arrived on time")]);

        var matches = await SearchAsync("deposit rejected");

        matches.Should().ContainSingle();
        matches[0].Session.Title.Text.Should().Be("holds both");
    }

    [Fact]
    public async Task SearchAsync_TwoWordsInOneMessage_OutranksTheSameWordsFoundApart()
    {
        await WriteSessionAsync(
            "found apart",
            chunks: [Prompt("the deposit never arrived"), AssistantText("the transfer was rejected by the bank")]);
        await WriteSessionAsync("found together", chunks: [Prompt("the deposit was rejected by the bank")]);

        var matches = await SearchAsync("deposit rejected");

        matches[0].Session.Title.Text.Should().Be("found together");
    }

    [Fact]
    public async Task SearchAsync_TitleMatch_OutranksTheSameTermInAnAssistantTurn()
    {
        await WriteSessionAsync("reconciliation", chunks: [Title("reconciliation")]);
        await WriteSessionAsync("other", chunks: [AssistantText("reconciliation happens in the nightly batch")]);

        var matches = await SearchAsync("reconciliation");

        matches[0].Session.Title.Text.Should().Be("reconciliation");
    }

    [Fact]
    public async Task SearchAsync_SeveralMatchingChunks_OutranksASingleMatchOfTheSameKind()
    {
        await WriteSessionAsync("mentioned once", chunks: [Prompt("the settlement failed")]);
        await WriteSessionAsync(
            "mentioned repeatedly",
            chunks: [Prompt("the settlement failed"), Prompt("the settlement failed again"), Prompt("settlement")]);

        var matches = await SearchAsync("settlement");

        matches[0].Session.Title.Text.Should().Be("mentioned repeatedly");
    }

    [Fact]
    public async Task SearchAsync_ManyWeakMatches_DoNotOutrankOneStrongMatchAtTheDefaultRepeatWeight()
    {
        await WriteLongAndPreciseSessionsAsync();

        var matches = await SearchAsync("settlement");

        matches[0].Session.Title.Text.Should().Be("precise");
    }

    [Fact]
    public async Task SearchAsync_RepeatsCounted_LetTheLongConversationWin()
    {
        await WriteLongAndPreciseSessionsAsync();

        var matches = await SearchAsync("settlement", repeatMatchWeight: 1.0);

        matches[0].Session.Title.Text.Should().Be("long winded");
    }

    [Fact]
    public async Task SearchAsync_QueryWithoutAccents_FindsAccentedText()
    {
        await WriteSessionAsync("accented", chunks: [Prompt("el depósito quedó pendiente")]);

        var matches = await SearchAsync("deposito");

        matches.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_QueryWithAccents_FindsUnaccentedText()
    {
        await WriteSessionAsync("unaccented", chunks: [Prompt("el deposito quedo pendiente")]);

        var matches = await SearchAsync("depósito");

        matches.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_PrefixOfAnIndexedWord_Matches()
    {
        await WriteSessionAsync("prefix", chunks: [Prompt("the reconciliation report")]);

        var matches = await SearchAsync("recon");

        matches.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_SeveralWords_RequiresEveryOneOfThem()
    {
        await WriteSessionAsync("both words", chunks: [Prompt("the nightly settlement report")]);
        await WriteSessionAsync("one word", chunks: [Prompt("the nightly backup")]);

        var matches = await SearchAsync("nightly settlement");

        matches.Should().ContainSingle();
        matches[0].Session.Title.Text.Should().Be("both words");
    }

    [Fact]
    public async Task SearchAsync_Match_ReportsWhichKindsOfTextMatched()
    {
        await WriteSessionAsync("settlement", chunks: [Title("settlement"), Prompt("settlement again")]);

        var matches = await SearchAsync("settlement");

        matches[0].MatchedKinds.Should().Equal(ChunkKind.Title, ChunkKind.UserPrompt);
        matches[0].MatchedChunkCount.Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_Match_ReturnsASnippetAroundTheMatchedTerm()
    {
        await WriteSessionAsync("snippet", chunks: [Prompt("the extraction was rejected by the bank")]);

        var matches = await SearchAsync("rejected");

        matches[0].Session.Snippet.Should().Contain("[rejected]");
    }

    [Fact]
    public async Task SearchAsync_Match_CarriesEverythingTheResultRowShows()
    {
        await WriteSessionAsync("row", chunks: [Prompt("the extraction was rejected")], branch: "main");

        var matches = await SearchAsync("extraction");

        var session = matches[0].Session;
        session.Folder.Display.Should().Be(@"C:\git\Example");
        session.Folder.Source.Should().Be(FolderSource.TranscriptCwd);
        session.Title.Source.Should().Be(TitleSource.AiTitle);
        session.GitBranch.Should().Be("main");
        session.LastActivity.Should().Be(Noon);
        session.MessageCount.Should().Be(7);
        session.FilePath.Should().EndWith(".jsonl");
    }

    [Fact]
    public async Task SearchAsync_EmptyText_ReturnsTheMostRecentlyActiveSessionsFirst()
    {
        await WriteSessionAsync("older", chunks: [Prompt("anything")], lastActivity: Noon.AddDays(-5));
        await WriteSessionAsync("newer", chunks: [Prompt("anything")], lastActivity: Noon);

        var matches = await SearchAsync(string.Empty);

        matches.Select(match => match.Session.Title.Text).Should().ContainInOrder("newer", "older");
    }

    [Fact]
    public async Task SearchAsync_EmptyText_ScoresNothingLexically()
    {
        await WriteSessionAsync("recent", chunks: [Prompt("anything")]);

        var matches = await SearchAsync(string.Empty);

        matches[0].LexicalScore.Should().Be(0);
        matches[0].MatchedKinds.Should().BeEmpty();
        matches[0].Session.Snippet.Should().BeNull();
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("*")]
    [InlineData("(")]
    [InlineData("-")]
    [InlineData("^")]
    [InlineData(":")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("NOT")]
    [InlineData(@"C:\git\Example")]
    [InlineData("\"unbalanced quote")]
    [InlineData("settlement AND (")]
    public async Task SearchAsync_TextTheQueryLanguageWouldChokeOn_IsAnsweredWithoutThrowing(string text)
    {
        await WriteSessionAsync("anything", chunks: [Prompt("the settlement report")]);

        var search = async () => await SearchAsync(text);

        await search.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SearchAsync_WindowsPath_FindsTheSessionStartedThere()
    {
        await WriteSessionAsync("in the folder", chunks: [Prompt("anything")]);

        var matches = await SearchAsync(@"C:\git\Example");

        matches.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_CandidateLimit_CapsHowManySessionsComeBack()
    {
        await WriteSessionAsync("first", chunks: [Prompt("the settlement report")]);
        await WriteSessionAsync("second", chunks: [Prompt("the settlement report")]);

        var matches = await SearchAsync("settlement", candidateLimit: 1);

        matches.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_TermThatIsNowhereInTheCorpus_ReturnsNothing()
    {
        await WriteSessionAsync("anything", chunks: [Prompt("the settlement report")]);

        var matches = await SearchAsync("zzzzunfindable");

        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_DatabaseNeverCreated_ReturnsNoCandidates()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "index.db");
        using var database = new SqliteIndexDatabase(Options.Create(new FinderOptions { IndexPath = missingPath }), NullLogger<SqliteIndexDatabase>.Instance);
        var reader = new SqliteSessionIndexReader(database);

        var matches = await reader.SearchAsync(
            Request("settlement", CandidateLimit, SearchOptions.DefaultRepeatMatchWeight),
            CancellationToken.None);

        matches.Should().BeEmpty();
    }

    /// <summary>
    /// The ranking statement binds a weight per stored chunk kind by its numeric value, so those
    /// values are part of the contract between the enum and the query.
    /// </summary>
    [Fact]
    public void ChunkKind_StoredValues_AreTheOnesTheRankingStatementWeighs()
    {
        ((int)ChunkKind.Title).Should().Be(1);
        ((int)ChunkKind.Folder).Should().Be(2);
        ((int)ChunkKind.LastPrompt).Should().Be(3);
        ((int)ChunkKind.UserPrompt).Should().Be(4);
        ((int)ChunkKind.AssistantText).Should().Be(5);
    }

    public void Dispose() => _index.Dispose();

    private async Task<IReadOnlyList<SessionMatch>> SearchAsync(
        string text,
        int candidateLimit = CandidateLimit,
        double repeatMatchWeight = SearchOptions.DefaultRepeatMatchWeight) =>
        await _index.Reader.SearchAsync(
            Request(text, candidateLimit, repeatMatchWeight),
            CancellationToken.None);

    private static SessionSearchRequest Request(string text, int candidateLimit, double repeatMatchWeight) => new()
    {
        Text = text,
        CandidateLimit = candidateLimit,
        Weights = ChunkWeights.Default,
        RepeatMatchWeight = repeatMatchWeight,
        CoOccurrenceWeight = SearchOptions.DefaultCoOccurrenceWeight,
    };

    private async Task WriteLongAndPreciseSessionsAsync()
    {
        await WriteSessionAsync("precise", chunks: [Title("settlement")]);
        await WriteSessionAsync(
            "long winded",
            chunks: [.. Enumerable.Range(0, 40).Select(turn => AssistantText($"the settlement of batch {turn}"))]);
    }

    private static SearchChunk Title(string text) => new(ChunkKind.Title, text, null);

    private static SearchChunk Prompt(string text) => new(ChunkKind.UserPrompt, text, null);

    private static SearchChunk AssistantText(string text) => new(ChunkKind.AssistantText, text, null);

    private async Task WriteSessionAsync(
        string title,
        IReadOnlyList<SearchChunk> chunks,
        DateTimeOffset? lastActivity = null,
        string? branch = null)
    {
        var sessionId = new SessionId(Guid.NewGuid());
        var folder = WorkingFolder.FromTranscriptCwd(@"C:\git\Example");

        var entry = new SessionIndexEntry
        {
            Document = new SessionDocument
            {
                SessionId = sessionId,
                Title = new SessionTitle(title, TitleSource.AiTitle),
                TitleCandidates = SessionTitleCandidates.ForFile($"{sessionId}.jsonl"),
                Folder = folder,
                GitBranch = branch,
                LastActivity = lastActivity ?? Noon,
                MessageCount = 7,
                ParseOffset = 4_096,
                Chunks = [.. chunks, FolderChunk(folder, branch)],
            },
            FilePath = $@"C:\projects\encoded-folder\{sessionId}.jsonl",
            Fingerprint = new FileFingerprint(4_096, 638_000_000_000_000_000, null, 4_096),
            IndexedAt = Noon,
        };

        await _index.Writer.WriteAsync(entry, CancellationToken.None);
        await _index.CheckpointAsync();
    }

    private static SearchChunk FolderChunk(WorkingFolder folder, string? branch) =>
        new(ChunkKind.Folder, branch is null ? folder.Display : $"{folder.Display} {branch}", null);
}
