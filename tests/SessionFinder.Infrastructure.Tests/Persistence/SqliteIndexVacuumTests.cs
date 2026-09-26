using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Infrastructure.Tests.Persistence;

/// <summary>
/// Reclaiming space is the one operation that cannot run inside a transaction, so it is asserted
/// against a real file rather than reasoned about.
/// </summary>
public sealed class SqliteIndexVacuumTests : IDisposable
{
    private const string HeadHash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";
    private const int SessionCount = 40;
    private const int ChunksPerSession = 20;

    private readonly TemporaryIndex _index = new();

    [Fact]
    public async Task VacuumAsync_SessionsWereDeleted_ReturnsTheFreedSpaceToTheFileSystem()
    {
        await FillAsync();
        await _index.Writer.PruneMissingAsync([], CancellationToken.None);
        await _index.CheckpointAsync();
        var beforeBytes = new FileInfo(_index.DatabasePath).Length;

        await _index.Writer.VacuumAsync(CancellationToken.None);

        new FileInfo(_index.DatabasePath).Length.Should().BeLessThan(beforeBytes);
    }

    [Fact]
    public async Task VacuumAsync_NothingWasDeleted_LeavesTheStoredSessionsReadable()
    {
        await FillAsync();

        await _index.Writer.VacuumAsync(CancellationToken.None);

        var stored = await _index.Writer.GetIndexedSessionAsync(SessionIdAt(0), CancellationToken.None);
        stored.Should().NotBeNull();
    }

    public void Dispose() => _index.Dispose();

    private async Task FillAsync()
    {
        for (var number = 0; number < SessionCount; number++)
        {
            await _index.Writer.WriteAsync(BuildEntry(number), CancellationToken.None);
        }
    }

    private static SessionIndexEntry BuildEntry(int number) => new()
    {
        Document = new SessionDocument
        {
            SessionId = SessionIdAt(number),
            Title = new SessionTitle($"session {number}", TitleSource.AiTitle),
            TitleCandidates = new SessionTitleCandidates { FileName = $"{SessionIdAt(number)}.jsonl" },
            Folder = WorkingFolder.FromTranscriptCwd(@"C:\git\Example"),
            MessageCount = ChunksPerSession,
            ParseOffset = 4_096,
            Chunks = BuildChunks(number),
        },
        FilePath = $@"C:\projects\encoded-folder\{SessionIdAt(number)}.jsonl",
        Fingerprint = new FileFingerprint(4_096, 638_000_000_000_000_000, HeadHash, 4_096),
        IndexedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
    };

    private static IReadOnlyList<SearchChunk> BuildChunks(int number) =>
    [
        .. Enumerable.Range(0, ChunksPerSession).Select(index => new SearchChunk(
            ChunkKind.AssistantText,
            $"session {number} chunk {index} " + new string('x', 400),
            null)),
    ];

    private static SessionId SessionIdAt(int number) =>
        new(new Guid(number, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]));
}
