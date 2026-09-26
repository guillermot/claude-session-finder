using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Infrastructure.Persistence;

namespace SessionFinder.Infrastructure.Tests.Persistence;

public sealed class SqliteSessionIndexReaderTests : IDisposable
{
    private const string SessionIdValue = "a2f8ba61-1f5f-4b0a-9a3e-7a2dfcf0f001";
    private const string HeadHash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";

    private readonly TemporaryIndex _index = new();

    [Fact]
    public async Task GetStatisticsAsync_DatabaseNeverCreated_ReportsAnEmptyIndex()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"), "index.db");
        using var database = new SqliteIndexDatabase(Options.Create(new FinderOptions { IndexPath = missingPath }), NullLogger<SqliteIndexDatabase>.Instance);
        var reader = new SqliteSessionIndexReader(database);

        var statistics = await reader.GetStatisticsAsync(CancellationToken.None);

        statistics.DatabaseExists.Should().BeFalse();
        statistics.SessionCount.Should().Be(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterOneSessionIsWritten_CountsIt()
    {
        await WriteSessionAsync();

        var statistics = await _index.Reader.GetStatisticsAsync(CancellationToken.None);

        statistics.DatabaseExists.Should().BeTrue();
        statistics.SessionCount.Should().Be(1);
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterOneSessionIsWritten_BreaksTheChunkCountDownByKind()
    {
        await WriteSessionAsync();

        var statistics = await _index.Reader.GetStatisticsAsync(CancellationToken.None);

        statistics.ChunkCount.Should().Be(3);
        statistics.ChunksByKind[ChunkKind.Title].Should().Be(1);
        statistics.ChunksByKind[ChunkKind.UserPrompt].Should().Be(2);
    }

    [Fact]
    public async Task GetStatisticsAsync_SessionIndexedWithDamagedLines_CountsItAsHavingAParseError()
    {
        await WriteSessionAsync(parseError: "2 line(s) skipped: not well-formed JSON.");

        var statistics = await _index.Reader.GetStatisticsAsync(CancellationToken.None);

        statistics.SessionsWithParseErrors.Should().Be(1);
    }

    [Fact]
    public async Task GetStatisticsAsync_SessionWhoseFolderCouldNotBeRecovered_CountsItAsUnknown()
    {
        await WriteSessionAsync(folder: WorkingFolder.Unknown);

        var statistics = await _index.Reader.GetStatisticsAsync(CancellationToken.None);

        statistics.SessionsWithUnknownFolder.Should().Be(1);
    }

    [Fact]
    public async Task GetStatisticsAsync_SessionWithAKnownFolder_IsNotCountedAsUnknown()
    {
        await WriteSessionAsync();

        var statistics = await _index.Reader.GetStatisticsAsync(CancellationToken.None);

        statistics.SessionsWithUnknownFolder.Should().Be(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_AfterAReconcileIsRecorded_ReportsWhenItCompleted()
    {
        var completedAt = new DateTimeOffset(2026, 3, 1, 12, 30, 0, TimeSpan.Zero);
        await _index.Writer.RecordReconcileAsync(completedAt, CancellationToken.None);
        await _index.CheckpointAsync();

        var statistics = await _index.Reader.GetStatisticsAsync(CancellationToken.None);

        statistics.LastReconcileAt.Should().Be(completedAt);
    }

    [Fact]
    public async Task GetStatisticsAsync_NoReconcileRecordedYet_ReportsNoLastRun()
    {
        await WriteSessionAsync();

        var statistics = await _index.Reader.GetStatisticsAsync(CancellationToken.None);

        statistics.LastReconcileAt.Should().BeNull();
    }

    [Fact]
    public async Task GetStatisticsAsync_SessionsWithActivity_ReportsTheNewestOne()
    {
        await WriteSessionAsync();

        var statistics = await _index.Reader.GetStatisticsAsync(CancellationToken.None);

        statistics.NewestActivity.Should().Be(new DateTimeOffset(2026, 2, 28, 11, 0, 0, TimeSpan.Zero));
    }

    public void Dispose() => _index.Dispose();

    private async Task WriteSessionAsync(string? parseError = null, WorkingFolder? folder = null)
    {
        await _index.Writer.WriteAsync(BuildEntry(parseError, folder), CancellationToken.None);
        await _index.CheckpointAsync();
    }

    private static SessionIndexEntry BuildEntry(string? parseError, WorkingFolder? folder) => new()
    {
        Document = new SessionDocument
        {
            SessionId = new SessionId(Guid.Parse(SessionIdValue)),
            Title = new SessionTitle("a generated title", TitleSource.AiTitle),
            TitleCandidates = SessionTitleCandidates.ForFile($"{SessionIdValue}.jsonl"),
            Folder = folder ?? WorkingFolder.FromTranscriptCwd(@"C:\git\Example"),
            LastActivity = new DateTimeOffset(2026, 2, 28, 11, 0, 0, TimeSpan.Zero),
            ParseOffset = 8_000,
            ParseError = parseError,
            Chunks =
            [
                new SearchChunk(ChunkKind.Title, "a generated title", null),
                new SearchChunk(ChunkKind.UserPrompt, "reconciling the pending deposits", null),
                new SearchChunk(ChunkKind.UserPrompt, "and the ones that settled late", null),
            ],
        },
        FilePath = $@"C:\projects\encoded-folder\{SessionIdValue}.jsonl",
        Fingerprint = new FileFingerprint(8_192, 638_000_000_000_000_000, HeadHash, 8_000),
        IndexedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
    };
}
