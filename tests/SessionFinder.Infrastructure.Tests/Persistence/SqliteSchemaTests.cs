using Microsoft.Data.Sqlite;
using SessionFinder.Infrastructure.Persistence;

namespace SessionFinder.Infrastructure.Tests.Persistence;

/// <summary>
/// Checks that the shipped schema behaves the way the spike said the chosen layout would.
/// </summary>
public sealed class SqliteSchemaTests : IDisposable
{
    private const string SessionId = "11111111-2222-3333-4444-555555555555";
    private const string OtherSessionId = "99999999-8888-7777-6666-555555555555";
    private const string InsertChunk =
        "INSERT INTO chunks(session_id, kind, ts, text) VALUES ($sessionId, $kind, $ts, $text);";
    private const string CountMatching = "SELECT count(*) FROM chunks_fts WHERE chunks_fts MATCH $query;";

    private readonly TemporaryDatabase _database;
    private readonly SqliteConnection _connection;

    public SqliteSchemaTests()
    {
        _database = new TemporaryDatabase();
        _connection = _database.OpenWriter();
    }

    [Fact]
    public async Task EnsureCurrentAsync_EmptyDatabase_CreatesTheSchema()
    {
        var created = await SqliteSchema.EnsureCurrentAsync(_connection, CancellationToken.None);

        created.Should().BeTrue();
        (await SqliteSchema.ReadUserVersionAsync(_connection, CancellationToken.None))
            .Should().Be(SqliteSchema.CurrentVersion);
    }

    [Fact]
    public async Task EnsureCurrentAsync_AlreadyCurrent_DoesNothing()
    {
        await SqliteSchema.EnsureCurrentAsync(_connection, CancellationToken.None);

        var createdAgain = await SqliteSchema.EnsureCurrentAsync(_connection, CancellationToken.None);

        createdAgain.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureCurrentAsync_UnknownVersion_DropsAndRebuilds()
    {
        await SqliteSchema.EnsureCurrentAsync(_connection, CancellationToken.None);
        InsertChunkFor(SessionId, "a deposit was reconciled");
        _connection.Execute("PRAGMA user_version = 999;");

        var rebuilt = await SqliteSchema.EnsureCurrentAsync(_connection, CancellationToken.None);

        rebuilt.Should().BeTrue();
        _connection.Scalar<long>("SELECT count(*) FROM chunks;").Should().Be(0);
    }

    [Fact]
    public async Task InsertChunk_Trigger_MakesTheTextSearchable()
    {
        await SqliteSchema.EnsureCurrentAsync(_connection, CancellationToken.None);

        InsertChunkFor(SessionId, "reconciling the depósito for the wallet");

        CountMatches("deposito").Should().Be(1);
    }

    [Fact]
    public async Task DeleteSessionChunks_Trigger_RemovesThemFromTheSearchIndex()
    {
        await SqliteSchema.EnsureCurrentAsync(_connection, CancellationToken.None);
        InsertChunkFor(SessionId, "reconciling the deposit for the wallet");
        InsertChunkFor(OtherSessionId, "a different deposit in another session");

        _connection.Execute("DELETE FROM chunks WHERE session_id = $sessionId;", ("$sessionId", SessionId));

        CountMatches("deposit").Should().Be(1);
    }

    [Fact]
    public async Task UpdateChunk_Trigger_ReplacesTheIndexedTextRatherThanAddingToIt()
    {
        await SqliteSchema.EnsureCurrentAsync(_connection, CancellationToken.None);
        InsertChunkFor(SessionId, "an outdated placeholder title");

        _connection.Execute(
            "UPDATE chunks SET text = $text WHERE session_id = $sessionId;",
            ("$text", "the resolved title"),
            ("$sessionId", SessionId));

        CountMatches("outdated").Should().Be(0);
        CountMatches("resolved").Should().Be(1);
    }

    [Fact]
    public async Task SelectChunkText_ExternalContentTable_StillReturnsTheStoredText()
    {
        await SqliteSchema.EnsureCurrentAsync(_connection, CancellationToken.None);
        InsertChunkFor(SessionId, "a deposit was reconciled");

        var stored = _connection.QueryStrings("SELECT text FROM chunks WHERE session_id = $sessionId;", ("$sessionId", SessionId));

        stored.Should().ContainSingle().Which.Should().Be("a deposit was reconciled");
    }

    public void Dispose()
    {
        _connection.Dispose();
        _database.Dispose();
    }

    private void InsertChunkFor(string sessionId, string text) =>
        _connection.Execute(
            InsertChunk,
            ("$sessionId", sessionId),
            ("$kind", 1),
            ("$ts", 0),
            ("$text", text));

    private long CountMatches(string userInput) =>
        _connection.Scalar<long>(CountMatching, ("$query", FtsQueryBuilder.Build(userInput).AnyTermExpression));
}
