using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;
using SessionFinder.Infrastructure.Persistence;

namespace SessionFinder.Infrastructure.Tests.Persistence;

public sealed class SqliteSessionIndexWriterTests : IDisposable
{
    private const string FirstSessionId = "a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001";
    private const string SecondSessionId = "b31c9d72-2a60-4c1b-8b4f-8b3efdf1e002";
    private const string HeadHash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";
    private const string CountMatchingChunks =
        "SELECT count(*) FROM chunks_fts WHERE chunks_fts MATCH $query;";

    private readonly TemporaryIndex _index = new();

    [Fact]
    public async Task WriteAsync_NewSession_StoresTheSessionRow()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        using var connection = _index.OpenInspector();
        connection.Scalar<long>("SELECT count(*) FROM sessions;").Should().Be(1);
    }

    [Fact]
    public async Task WriteAsync_NewSession_StoresEveryChunkItCarries()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        using var connection = _index.OpenInspector();
        connection.Scalar<long>("SELECT count(*) FROM chunks;").Should().Be(3);
    }

    [Fact]
    public async Task WriteAsync_NewSession_MakesItsChunksSearchable()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        using var connection = _index.OpenInspector();
        connection
            .Scalar<long>(CountMatchingChunks, ("$query", FtsQueryBuilder.Build("reconciling").AnyTermExpression))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task GetIndexedSessionAsync_AfterWrite_RoundTripsTheFingerprint()
    {
        var entry = BuildEntry();
        await _index.Writer.WriteAsync(entry, CancellationToken.None);

        var stored = await _index.Writer.GetIndexedSessionAsync(BuildSessionId(FirstSessionId), CancellationToken.None);

        stored!.Fingerprint.Should().Be(entry.Fingerprint);
    }

    [Fact]
    public async Task GetIndexedSessionAsync_AfterWrite_RoundTripsTheTitleCandidates()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        var stored = await _index.Writer.GetIndexedSessionAsync(BuildSessionId(FirstSessionId), CancellationToken.None);

        stored!.TitleCandidates.AiTitle.Should().Be("a generated title");
        stored.TitleCandidates.FirstPrompt.Should().Be("reconciling the pending deposits");
    }

    [Fact]
    public async Task GetIndexedSessionAsync_AfterWrite_RoundTripsTheFolderIncludingItsCasing()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        var stored = await _index.Writer.GetIndexedSessionAsync(BuildSessionId(FirstSessionId), CancellationToken.None);

        stored!.Folder.Display.Should().Be(@"C:\git\Example");
        stored.Folder.Source.Should().Be(FolderSource.TranscriptCwd);
    }

    [Fact]
    public async Task GetIndexedSessionAsync_SessionThatWasNeverIndexed_ReportsNothing()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        var stored = await _index.Writer.GetIndexedSessionAsync(
            BuildSessionId(SecondSessionId),
            CancellationToken.None);

        stored.Should().BeNull();
    }

    [Fact]
    public async Task WriteAsync_SessionWrittenTwice_ReplacesItsChunksRatherThanAddingToThem()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        using var connection = _index.OpenInspector();
        connection.Scalar<long>("SELECT count(*) FROM chunks;").Should().Be(3);
    }

    [Fact]
    public async Task WriteAsync_ChunkTextThatDisappeared_IsNoLongerSearchable()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);
        var reparsed = BuildEntry() with { Document = BuildDocument() with { Chunks = [] } };

        await _index.Writer.WriteAsync(reparsed, CancellationToken.None);

        using var connection = _index.OpenInspector();
        connection
            .Scalar<long>(CountMatchingChunks, ("$query", FtsQueryBuilder.Build("reconciling").AnyTermExpression))
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task ReviseTitleAsync_NewerTitle_UpdatesTheStoredTitle()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        await _index.Writer.ReviseTitleAsync(BuildRevision(), CancellationToken.None);

        var stored = await _index.Writer.GetIndexedSessionAsync(BuildSessionId(FirstSessionId), CancellationToken.None);
        stored!.Title.Text.Should().Be("a title the user typed");
        stored.Title.Source.Should().Be(TitleSource.CustomTitle);
    }

    [Fact]
    public async Task ReviseTitleAsync_NewerTitle_ReplacesTheTitleChunkWithoutTouchingTheOthers()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        await _index.Writer.ReviseTitleAsync(BuildRevision(), CancellationToken.None);

        using var connection = _index.OpenInspector();
        connection.Scalar<long>("SELECT count(*) FROM chunks;").Should().Be(3);
        connection
            .QueryStrings("SELECT text FROM chunks WHERE kind = $kind;", ("$kind", (int)ChunkKind.Title))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("a title the user typed");
    }

    [Fact]
    public async Task ReviseTitleAsync_NewerTitle_RemovesTheOldTitleFromTheSearchIndex()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        await _index.Writer.ReviseTitleAsync(BuildRevision(), CancellationToken.None);

        using var connection = _index.OpenInspector();
        connection
            .Scalar<long>(CountMatchingChunks, ("$query", FtsQueryBuilder.Build("generated").AnyTermExpression))
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task ReviseTitleAsync_NewerTitle_AdvancesTheStoredWatermark()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        await _index.Writer.ReviseTitleAsync(BuildRevision(), CancellationToken.None);

        var stored = await _index.Writer.GetIndexedSessionAsync(BuildSessionId(FirstSessionId), CancellationToken.None);
        stored!.Fingerprint.ParseOffset.Should().Be(9_000);
    }

    [Fact]
    public async Task ReviseTitleAsync_SessionThatWasNeverIndexed_ReportsNoUpdate()
    {
        var revision = BuildRevision() with { SessionId = BuildSessionId(SecondSessionId) };

        var updated = await _index.Writer.ReviseTitleAsync(revision, CancellationToken.None);

        updated.Should().BeFalse();
    }

    [Fact]
    public async Task PruneMissingAsync_SessionNoLongerOnDisk_RemovesTheSessionRow()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);
        await _index.Writer.WriteAsync(BuildSecondEntry(), CancellationToken.None);

        var pruned = await _index.Writer.PruneMissingAsync(
            [BuildSessionId(FirstSessionId)],
            CancellationToken.None);

        pruned.Should().Be(1);
        using var connection = _index.OpenInspector();
        connection.Scalar<long>("SELECT count(*) FROM sessions;").Should().Be(1);
    }

    [Fact]
    public async Task PruneMissingAsync_SessionNoLongerOnDisk_RemovesItsChunksFromTheSearchIndex()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);
        await _index.Writer.WriteAsync(BuildSecondEntry(), CancellationToken.None);

        await _index.Writer.PruneMissingAsync([BuildSessionId(FirstSessionId)], CancellationToken.None);

        using var connection = _index.OpenInspector();
        connection
            .Scalar<long>(CountMatchingChunks, ("$query", FtsQueryBuilder.Build("withdrawal").AnyTermExpression))
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task PruneMissingAsync_EveryIndexedSessionStillOnDisk_RemovesNothing()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);

        var pruned = await _index.Writer.PruneMissingAsync(
            [BuildSessionId(FirstSessionId), BuildSessionId(SecondSessionId)],
            CancellationToken.None);

        pruned.Should().Be(0);
    }

    [Fact]
    public async Task CompactAsync_AfterChunksWereReplaced_KeepsTheRemainingTextSearchable()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);
        await _index.Writer.WriteAsync(BuildSecondEntry(), CancellationToken.None);

        await _index.Writer.CompactAsync(CancellationToken.None);

        using var connection = _index.OpenInspector();
        connection
            .Scalar<long>(CountMatchingChunks, ("$query", FtsQueryBuilder.Build("reconciling").AnyTermExpression))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task CompactAsync_AfterChunksWereRemoved_DoesNotBringThemBack()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);
        await _index.Writer.WriteAsync(BuildSecondEntry(), CancellationToken.None);
        await _index.Writer.PruneMissingAsync([BuildSessionId(FirstSessionId)], CancellationToken.None);

        await _index.Writer.CompactAsync(CancellationToken.None);

        using var connection = _index.OpenInspector();
        connection
            .Scalar<long>(CountMatchingChunks, ("$query", FtsQueryBuilder.Build("withdrawal").AnyTermExpression))
            .Should()
            .Be(0);
    }

    [Fact]
    public async Task WriteAsync_SessionMovedToAnotherProjectDirectory_UpdatesItsPath()
    {
        await _index.Writer.WriteAsync(BuildEntry(), CancellationToken.None);
        var moved = BuildEntry() with { FilePath = $@"C:\projects\other-folder\{FirstSessionId}.jsonl" };

        await _index.Writer.WriteAsync(moved, CancellationToken.None);

        var stored = await _index.Writer.GetIndexedSessionAsync(BuildSessionId(FirstSessionId), CancellationToken.None);
        stored!.FilePath.Should().Be(moved.FilePath);
    }

    public void Dispose() => _index.Dispose();

    private static SessionIndexEntry BuildEntry() => new()
    {
        Document = BuildDocument(),
        FilePath = $@"C:\projects\encoded-folder\{FirstSessionId}.jsonl",
        Fingerprint = new FileFingerprint(8_192, 638_000_000_000_000_000, HeadHash, 8_000),
        IndexedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
    };

    private static SessionIndexEntry BuildSecondEntry() => new()
    {
        Document = BuildDocument() with
        {
            SessionId = BuildSessionId(SecondSessionId),
            Chunks = [new SearchChunk(ChunkKind.UserPrompt, "a withdrawal that never settled", null)],
        },
        FilePath = $@"C:\projects\encoded-folder\{SecondSessionId}.jsonl",
        Fingerprint = new FileFingerprint(2_048, 638_000_000_000_000_000, HeadHash, 2_048),
        IndexedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
    };

    private static SessionDocument BuildDocument() => new()
    {
        SessionId = BuildSessionId(FirstSessionId),
        Title = new SessionTitle("a generated title", TitleSource.AiTitle),
        TitleCandidates = new SessionTitleCandidates
        {
            FileName = $"{FirstSessionId}.jsonl",
            AiTitle = "a generated title",
            FirstPrompt = "reconciling the pending deposits",
        },
        Folder = WorkingFolder.FromTranscriptCwd(@"C:\git\Example"),
        GitBranch = "main",
        MessageCount = 12,
        FirstActivity = new DateTimeOffset(2026, 2, 28, 9, 0, 0, TimeSpan.Zero),
        LastActivity = new DateTimeOffset(2026, 2, 28, 11, 0, 0, TimeSpan.Zero),
        ParseOffset = 8_000,
        Chunks =
        [
            new SearchChunk(ChunkKind.Title, "a generated title", null),
            new SearchChunk(ChunkKind.Folder, @"C:\git\Example main", null),
            new SearchChunk(ChunkKind.UserPrompt, "reconciling the pending deposits", null),
        ],
    };

    private static SessionTitleRevision BuildRevision() => new()
    {
        SessionId = BuildSessionId(FirstSessionId),
        Title = new SessionTitle("a title the user typed", TitleSource.CustomTitle),
        Candidates = new SessionTitleCandidates
        {
            FileName = $"{FirstSessionId}.jsonl",
            CustomTitle = "a title the user typed",
            AiTitle = "a generated title",
            FirstPrompt = "reconciling the pending deposits",
        },
        Fingerprint = new FileFingerprint(9_216, 638_000_000_000_000_001, HeadHash, 9_000),
        IndexedAt = new DateTimeOffset(2026, 3, 1, 11, 0, 0, TimeSpan.Zero),
    };

    private static SessionId BuildSessionId(string value) => new(Guid.Parse(value));
}
