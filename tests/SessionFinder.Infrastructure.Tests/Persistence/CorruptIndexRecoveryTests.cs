using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Domain;
using SessionFinder.Infrastructure.Persistence;

namespace SessionFinder.Infrastructure.Tests.Persistence;

/// <summary>
/// The index is a derived cache of files that are still on disk, so a file that is no longer a
/// database is thrown away rather than mourned. These tests corrupt a real one on purpose.
/// </summary>
public sealed class CorruptIndexRecoveryTests : IDisposable
{
    private const string SessionId = "c4a1d5e6-3b71-4d2c-9c5f-9c4fefe2f003";
    private const string HeadHash = "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08";
    private const string DatabaseFileName = "index.db";
    private const string RootFolderName = "session-finder-corrupt";
    private const int GarbageLength = 8_192;

    private readonly string _directory;

    public CorruptIndexRecoveryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), RootFolderName, Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_directory);

        DatabasePath = Path.Combine(_directory, DatabaseFileName);
    }

    private string DatabasePath { get; }

    [Fact]
    public async Task GetWriterConnectionAsync_TheFileIsNotADatabase_OpensAnEmptyOneInstead()
    {
        WriteGarbage();

        await using var database = CreateDatabase();
        var connection = await database.GetWriterConnectionAsync(CancellationToken.None);

        connection.State.Should().Be(System.Data.ConnectionState.Open);
    }

    [Fact]
    public async Task GetWriterConnectionAsync_TheFileIsNotADatabase_LeavesTheSchemaInPlace()
    {
        WriteGarbage();

        await using var database = CreateDatabase();
        var writer = new SqliteSessionIndexWriter(database);
        await writer.WriteAsync(BuildEntry(), CancellationToken.None);

        var stored = await writer.GetIndexedSessionAsync(BuildSessionId(), CancellationToken.None);
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task RebuildAsync_AnIndexWithSessionsInIt_ComesBackEmpty()
    {
        await using var database = CreateDatabase();
        var writer = new SqliteSessionIndexWriter(database);
        await writer.WriteAsync(BuildEntry(), CancellationToken.None);

        await database.RebuildAsync(CancellationToken.None);

        var stored = await writer.GetIndexedSessionAsync(BuildSessionId(), CancellationToken.None);
        stored.Should().BeNull();
    }

    [Fact]
    public async Task RebuildAsync_AfterRebuilding_TheIndexIsWritableAgain()
    {
        await using var database = CreateDatabase();
        var writer = new SqliteSessionIndexWriter(database);
        await database.RebuildAsync(CancellationToken.None);

        await writer.WriteAsync(BuildEntry(), CancellationToken.None);

        var stored = await writer.GetIndexedSessionAsync(BuildSessionId(), CancellationToken.None);
        stored.Should().NotBeNull();
    }

    [Fact]
    public void IsCorruption_TheDatabaseIsLockedRatherThanBroken_SaysNo()
    {
        var busy = new SqliteException("database is locked", 5);

        SqliteIndexDatabase.IsCorruption(busy).Should().BeFalse();
    }

    [Fact]
    public void IsCorruption_TheFileIsNotADatabase_SaysYes()
    {
        var notADatabase = new SqliteException("file is not a database", 26);

        SqliteIndexDatabase.IsCorruption(notADatabase).Should().BeTrue();
    }

    [Fact]
    public void IsCorruption_TheDiskImageIsMalformed_SaysYes()
    {
        var malformed = new SqliteException("database disk image is malformed", 11);

        SqliteIndexDatabase.IsCorruption(malformed).Should().BeTrue();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
    }

    private SqliteIndexDatabase CreateDatabase() =>
        new(Options.Create(new FinderOptions { IndexPath = DatabasePath }), NullLogger<SqliteIndexDatabase>.Instance);

    /// <summary>
    /// Writes a file with a valid name, a plausible size and nothing a database can read, which is
    /// what a half-flushed file or a bad sector leaves behind.
    /// </summary>
    private void WriteGarbage()
    {
        var garbage = new byte[GarbageLength];
        Random.Shared.NextBytes(garbage);

        File.WriteAllBytes(DatabasePath, garbage);
    }

    private static SessionIndexEntry BuildEntry() => new()
    {
        Document = new SessionDocument
        {
            SessionId = BuildSessionId(),
            Title = new SessionTitle("a rebuilt session", TitleSource.AiTitle),
            TitleCandidates = new SessionTitleCandidates { FileName = $"{SessionId}.jsonl" },
            Folder = WorkingFolder.FromTranscriptCwd(@"C:\git\Example"),
            MessageCount = 1,
            ParseOffset = 128,
            Chunks = [new SearchChunk(ChunkKind.UserPrompt, "a prompt", null)],
        },
        FilePath = $@"C:\projects\encoded-folder\{SessionId}.jsonl",
        Fingerprint = new FileFingerprint(128, 638_000_000_000_000_000, HeadHash, 128),
        IndexedAt = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero),
    };

    private static SessionId BuildSessionId() => new(Guid.Parse(SessionId));
}
