using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace SessionFinder.Infrastructure.Tests.Persistence;

/// <summary>
/// Decides how the index removes one session's chunks. Three candidate FTS5 layouts are built
/// from the same corpus and the same deletions are timed against each, because the answer
/// depends on the SQLite build that ships inside the package, not on documentation.
/// </summary>
/// <remarks>
/// The corpus grows by adding sessions rather than by enlarging them, so that the effect of total
/// table size is separated from the effect of how much is being deleted. Measured result: FTS5
/// delete cost grows with total index size under every strategy, because removing a document
/// rewrites posting lists across a larger index; no layout gives a genuinely flat per-session
/// delete. What the layout does change is the constant factor, and that gap is what is asserted.
/// </remarks>
public sealed class FtsDeleteStrategyTests
{
    private const int ChunksPerSession = 333;
    private const int BaselineSessionCount = 150;
    private const int ScaledSessionCount = 450;
    private const int MeasuredDeletions = 30;
    private const int WordsPerChunk = 24;
    private const int KindCount = 4;
    private const double RequiredMedianAdvantage = 1.25;
    private const string TokenizerOptions = "unicode61 remove_diacritics 2";
    private const string PrefixIndexSizes = "2 3 4";
    private const string SessionMarkerPrefix = "sessionmarker";
    private const string SessionIndexName = "ix_ec_chunks_session_id";
    private const string BenchmarkTrait = "Category";
    private const string BenchmarkCategory = "Benchmark";
    private const string DeleteFromContentOwningTable = "DELETE FROM plain_fts WHERE session_id = $sessionId;";
    private const string DeleteFromExternalContentTable = "DELETE FROM ec_chunks WHERE session_id = $sessionId;";
    private const int QueryPlanDetailColumn = 3;

    private static readonly string[] Vocabulary =
    [
        "deposit", "withdrawal", "reconciliation", "transfer", "wallet", "project", "settlement",
        "account", "balance", "pending", "approved", "rejected", "callback", "webhook", "retry",
        "timeout", "circuit", "breaker", "idempotency", "partition", "consumer", "producer",
        "offset", "checkpoint", "watermark", "snapshot", "migration", "schema", "index", "query",
        "handler", "repository", "gateway", "listener", "scheduler", "dispatcher", "envelope",
        "payload", "correlation", "trace",
    ];

    private readonly ITestOutputHelper _output;

    public FtsDeleteStrategyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void CreateTable_ContentlessDeleteOption_IsAcceptedByTheBundledSqlite()
    {
        using var database = new TemporaryDatabase();
        using var connection = database.OpenWriter();
        var version = connection.Scalar<string>("SELECT sqlite_version();");

        var createContentlessDeleteTable = () => connection.Execute(
            $"""
             CREATE VIRTUAL TABLE probe_fts USING fts5(
                 text,
                 content='',
                 contentless_delete=1,
                 tokenize='{TokenizerOptions}'
             );
             """);

        _output.WriteLine($"SQLite version : {version} (contentless_delete requires 3.43.0 or newer)");
        createContentlessDeleteTable.Should().NotThrow();
    }

    [Fact]
    public void Select_ContentlessTable_ReturnsNullInsteadOfTheIndexedText()
    {
        using var database = new TemporaryDatabase();
        using var connection = database.OpenWriter();
        connection.Execute(
            $"""
             CREATE VIRTUAL TABLE cd_fts USING fts5(
                 text,
                 content='',
                 contentless_delete=1,
                 tokenize='{TokenizerOptions}'
             );
             """);
        connection.Execute("INSERT INTO cd_fts(rowid, text) VALUES (1, 'a deposit was reconciled');");

        var matched = connection.QueryNullableStrings(
            "SELECT text FROM cd_fts WHERE cd_fts MATCH $query;",
            ("$query", "\"deposit\""));

        _output.WriteLine($"contentless table returned {matched.Count} row(s), text = {matched[0] ?? "<null>"}");
        matched.Should().ContainSingle().Which.Should().BeNull(
            "a contentless table can be matched and counted but cannot give the text back for snippets");
    }

    [Fact]
    public void DeleteSessionChunks_ContentOwningLayout_ScansTheWholeSearchTable()
    {
        using var database = new TemporaryDatabase();
        using var connection = database.OpenWriter();
        CreateContentOwningLayout(connection);

        var plan = ExplainQueryPlan(connection, DeleteFromContentOwningTable, BuildSessionId(0));

        ReportQueryPlan("content-owning FTS5, delete by UNINDEXED column", plan);
        plan.Should().Contain(
            step => step.Contains("SCAN", StringComparison.Ordinal),
            "an UNINDEXED column is stored but not indexed, so every row is visited before any is removed");
    }

    [Fact]
    public void DeleteSessionChunks_ExternalContentLayout_ReachesOnlyTheRowsItRemoves()
    {
        using var database = new TemporaryDatabase();
        using var connection = database.OpenWriter();
        CreateExternalContentLayout(connection);

        var plan = ExplainQueryPlan(connection, DeleteFromExternalContentTable, BuildSessionId(0));

        ReportQueryPlan("external content over a base chunks table", plan);
        plan.Should().Contain(
            step => step.Contains(SessionIndexName, StringComparison.Ordinal),
            "the base table carries an ordinary index on the session, which is the whole point of the layout");
        plan.Should().NotContain(
            step => step.Contains("SCAN", StringComparison.Ordinal),
            "a delete that scans defeats the layout no matter how fast the machine running it is");
    }

    [Trait(BenchmarkTrait, BenchmarkCategory)]
    [Fact]
    public void DeleteSessionChunks_GrowingCorpus_FavoursExternalContentOverScanningAContentOwningTable()
    {
        var baseline = new Corpus(BaselineSessionCount, ChunksPerSession);
        var scaled = new Corpus(ScaledSessionCount, ChunksPerSession);

        var baselineResults = MeasureEveryStrategy(baseline);
        var scaledResults = MeasureEveryStrategy(scaled);

        ReportMeasurements(baseline, baselineResults);
        ReportMeasurements(scaled, scaledResults);
        ReportScaling(baseline, baselineResults, scaled, scaledResults);

        scaledResults.ExternalContent.MedianDeleteMilliseconds.Should()
            .BeLessThan(
                scaledResults.ContentOwning.MedianDeleteMilliseconds / RequiredMedianAdvantage,
                "filtering an FTS5 table by an UNINDEXED column scans every row before it can delete any");
        scaledResults.Contentless.MedianDeleteMilliseconds.Should()
            .BeLessThan(
                scaledResults.ContentOwning.MedianDeleteMilliseconds / RequiredMedianAdvantage,
                "a rowid-keyed delete reaches only the rows it removes");
    }

    private StrategyComparison MeasureEveryStrategy(Corpus corpus) => new(
        MeasureContentOwningStrategy(corpus),
        MeasureContentlessDeleteStrategy(corpus),
        MeasureExternalContentStrategy(corpus));

    private StrategyMeasurement MeasureContentOwningStrategy(Corpus corpus)
    {
        const string InsertChunk =
            "INSERT INTO plain_fts(rowid, text, session_id, kind, ts) VALUES ($id, $text, $sessionId, $kind, $ts);";
        const string CountMatching = "SELECT count(*) FROM plain_fts WHERE plain_fts MATCH $query;";

        using var database = new TemporaryDatabase();
        using var connection = database.OpenWriter();
        CreateContentOwningLayout(connection);

        var populateElapsed = Measure(() => PopulateInOneTransaction(connection, InsertChunk, BindFullRow, corpus));
        var deleteElapsed = MeasureDeletions(
            connection,
            sessionId => connection.Execute(DeleteFromContentOwningTable, ("$sessionId", sessionId)));

        AssertDeletedSessionIsGone(connection, CountMatching);

        return Describe("content-owning FTS5, delete by UNINDEXED column", database, populateElapsed, deleteElapsed);
    }

    private StrategyMeasurement MeasureContentlessDeleteStrategy(Corpus corpus)
    {
        const string InsertChunk = "INSERT INTO cd_fts(rowid, text) VALUES ($id, $text);";
        const string InsertMapRow =
            "INSERT INTO cd_chunk_map(chunk_id, session_id, kind, ts) VALUES ($id, $sessionId, $kind, $ts);";
        const string DeleteBySession =
            """
            DELETE FROM cd_fts
            WHERE rowid IN (SELECT chunk_id FROM cd_chunk_map WHERE session_id = $sessionId);
            """;
        const string DeleteMapRows = "DELETE FROM cd_chunk_map WHERE session_id = $sessionId;";
        const string CountMatching = "SELECT count(*) FROM cd_fts WHERE cd_fts MATCH $query;";

        using var database = new TemporaryDatabase();
        using var connection = database.OpenWriter();

        connection.Execute(
            $"""
             CREATE VIRTUAL TABLE cd_fts USING fts5(
                 text,
                 content='',
                 contentless_delete=1,
                 tokenize='{TokenizerOptions}',
                 prefix='{PrefixIndexSizes}'
             );
             """);
        connection.Execute(
            """
            CREATE TABLE cd_chunk_map(
                chunk_id   INTEGER PRIMARY KEY,
                session_id TEXT    NOT NULL,
                kind       INTEGER NOT NULL,
                ts         INTEGER NOT NULL
            );
            """);
        connection.Execute("CREATE INDEX ix_cd_chunk_map_session_id ON cd_chunk_map(session_id);");

        var populateElapsed = Measure(() =>
        {
            PopulateInOneTransaction(connection, InsertChunk, BindTextOnlyRow, corpus);
            PopulateInOneTransaction(connection, InsertMapRow, BindMetadataRow, corpus);
        });

        var deleteElapsed = MeasureDeletions(connection, sessionId =>
        {
            connection.Execute(DeleteBySession, ("$sessionId", sessionId));
            connection.Execute(DeleteMapRows, ("$sessionId", sessionId));
        });

        AssertDeletedSessionIsGone(connection, CountMatching);

        return Describe("contentless_delete=1 plus a rowid side table", database, populateElapsed, deleteElapsed);
    }

    private StrategyMeasurement MeasureExternalContentStrategy(Corpus corpus)
    {
        const string InsertChunk =
            "INSERT INTO ec_chunks(id, session_id, kind, ts, text) VALUES ($id, $sessionId, $kind, $ts, $text);";
        const string CountMatching = "SELECT count(*) FROM ec_fts WHERE ec_fts MATCH $query;";

        using var database = new TemporaryDatabase();
        using var connection = database.OpenWriter();
        CreateExternalContentLayout(connection);

        var populateElapsed = Measure(() => PopulateInOneTransaction(connection, InsertChunk, BindFullRow, corpus));
        var deleteElapsed = MeasureDeletions(
            connection,
            sessionId => connection.Execute(DeleteFromExternalContentTable, ("$sessionId", sessionId)));

        AssertDeletedSessionIsGone(connection, CountMatching);

        return Describe("external content over a base chunks table", database, populateElapsed, deleteElapsed);
    }

    /// <summary>
    /// The layout that keeps the metadata inside the search table, where the session column is
    /// stored but not indexed.
    /// </summary>
    private static void CreateContentOwningLayout(SqliteConnection connection) => connection.Execute(
        $"""
         CREATE VIRTUAL TABLE plain_fts USING fts5(
             text,
             session_id UNINDEXED,
             kind UNINDEXED,
             ts UNINDEXED,
             tokenize='{TokenizerOptions}',
             prefix='{PrefixIndexSizes}'
         );
         """);

    /// <summary>
    /// The layout the index uses: an ordinary table owning the text and the metadata, kept in step
    /// with a search table by triggers.
    /// </summary>
    private static void CreateExternalContentLayout(SqliteConnection connection)
    {
        connection.Execute(
            """
            CREATE TABLE ec_chunks(
                id         INTEGER PRIMARY KEY,
                session_id TEXT    NOT NULL,
                kind       INTEGER NOT NULL,
                ts         INTEGER NOT NULL,
                text       TEXT    NOT NULL
            );
            """);
        connection.Execute($"CREATE INDEX {SessionIndexName} ON ec_chunks(session_id);");
        connection.Execute(
            $"""
             CREATE VIRTUAL TABLE ec_fts USING fts5(
                 text,
                 content='ec_chunks',
                 content_rowid='id',
                 tokenize='{TokenizerOptions}',
                 prefix='{PrefixIndexSizes}'
             );
             """);
        connection.Execute(
            """
            CREATE TRIGGER ec_chunks_after_insert AFTER INSERT ON ec_chunks BEGIN
                INSERT INTO ec_fts(rowid, text) VALUES (new.id, new.text);
            END;
            """);
        connection.Execute(
            """
            CREATE TRIGGER ec_chunks_after_delete AFTER DELETE ON ec_chunks BEGIN
                INSERT INTO ec_fts(ec_fts, rowid, text) VALUES ('delete', old.id, old.text);
            END;
            """);
        connection.Execute(
            """
            CREATE TRIGGER ec_chunks_after_update AFTER UPDATE ON ec_chunks BEGIN
                INSERT INTO ec_fts(ec_fts, rowid, text) VALUES ('delete', old.id, old.text);
                INSERT INTO ec_fts(rowid, text) VALUES (new.id, new.text);
            END;
            """);
    }

    /// <summary>
    /// Asks the query planner how it intends to satisfy a statement, which answers the question the
    /// timings only approximate: whether the delete reaches its rows or walks past everything else
    /// to find them.
    /// </summary>
    private static List<string> ExplainQueryPlan(SqliteConnection connection, string sql, string sessionId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;
        command.Parameters.AddWithValue("$sessionId", sessionId);

        using var reader = command.ExecuteReader();
        var steps = new List<string>();

        while (reader.Read())
        {
            steps.Add(reader.GetString(QueryPlanDetailColumn));
        }

        return steps;
    }

    private void ReportQueryPlan(string layout, IEnumerable<string> steps)
    {
        _output.WriteLine(layout);

        foreach (var step in steps)
        {
            _output.WriteLine($"  {step}");
        }
    }

    private static void PopulateInOneTransaction(
        SqliteConnection connection,
        string insertSql,
        Action<SqliteCommand, ChunkRow> bind,
        Corpus corpus)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = insertSql;

        foreach (var chunk in corpus.BuildChunks())
        {
            command.Parameters.Clear();
            bind(command, chunk);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void BindFullRow(SqliteCommand command, ChunkRow chunk)
    {
        command.Parameters.AddWithValue("$id", chunk.Id);
        command.Parameters.AddWithValue("$sessionId", chunk.SessionId);
        command.Parameters.AddWithValue("$kind", chunk.Kind);
        command.Parameters.AddWithValue("$ts", chunk.Timestamp);
        command.Parameters.AddWithValue("$text", chunk.Text);
    }

    private static void BindTextOnlyRow(SqliteCommand command, ChunkRow chunk)
    {
        command.Parameters.AddWithValue("$id", chunk.Id);
        command.Parameters.AddWithValue("$text", chunk.Text);
    }

    private static void BindMetadataRow(SqliteCommand command, ChunkRow chunk)
    {
        command.Parameters.AddWithValue("$id", chunk.Id);
        command.Parameters.AddWithValue("$sessionId", chunk.SessionId);
        command.Parameters.AddWithValue("$kind", chunk.Kind);
        command.Parameters.AddWithValue("$ts", chunk.Timestamp);
    }

    private static IReadOnlyList<double> MeasureDeletions(SqliteConnection connection, Action<string> deleteOneSession)
    {
        var elapsed = new List<double>(MeasuredDeletions);

        for (var sessionIndex = 0; sessionIndex < MeasuredDeletions; sessionIndex++)
        {
            var sessionId = BuildSessionId(sessionIndex);
            using var transaction = connection.BeginTransaction();
            var stopwatch = Stopwatch.StartNew();

            deleteOneSession(sessionId);
            transaction.Commit();

            elapsed.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return elapsed;
    }

    private static void AssertDeletedSessionIsGone(SqliteConnection connection, string countMatchingSql)
    {
        var remaining = connection.Scalar<long>(countMatchingSql, ("$query", $"\"{BuildSessionMarker(0)}\""));

        remaining.Should().Be(0, "a deleted session must leave no searchable chunk behind");
    }

    private static double Measure(Action work)
    {
        var stopwatch = Stopwatch.StartNew();
        work();

        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static StrategyMeasurement Describe(
        string name,
        TemporaryDatabase database,
        double populateMilliseconds,
        IReadOnlyList<double> deleteMilliseconds)
    {
        var ordered = deleteMilliseconds.Order().ToArray();

        return new StrategyMeasurement(
            name,
            populateMilliseconds,
            database.FileSizeBytes,
            deleteMilliseconds.Sum(),
            deleteMilliseconds.Average(),
            ordered[ordered.Length / 2],
            ordered[^1]);
    }

    private void ReportMeasurements(Corpus corpus, StrategyComparison comparison)
    {
        _output.WriteLine(string.Empty);
        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"corpus: {corpus.ChunkCount:N0} chunks across {corpus.SessionCount:N0} sessions ({corpus.ChunksPerSession} each); {MeasuredDeletions} sessions deleted one at a time"));
        _output.WriteLine(
            $"{"strategy",-46} {"populate ms",12} {"db MB",8} {"del total",10} {"del mean",9} {"del p50",9} {"del max",9}");

        foreach (var measurement in comparison.All)
        {
            _output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{measurement.Name,-46} {measurement.PopulateMilliseconds,12:N0} {measurement.DatabaseMegabytes,8:N1} {measurement.TotalDeleteMilliseconds,10:N1} {measurement.MeanDeleteMilliseconds,9:N2} {measurement.MedianDeleteMilliseconds,9:N2} {measurement.MaxDeleteMilliseconds,9:N2}"));
        }
    }

    private void ReportScaling(
        Corpus baselineCorpus,
        StrategyComparison baseline,
        Corpus scaledCorpus,
        StrategyComparison scaled)
    {
        var corpusGrowth = (double)scaledCorpus.ChunkCount / baselineCorpus.ChunkCount;

        _output.WriteLine(string.Empty);
        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"corpus grew {corpusGrowth:N1}x with session size held constant; median delete cost grew by:"));

        foreach (var (before, after) in baseline.All.Zip(scaled.All))
        {
            _output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{before.Name,-46} {before.MedianDeleteMilliseconds,9:N2} -> {after.MedianDeleteMilliseconds,9:N2} ms  ({after.MedianDeleteMilliseconds / before.MedianDeleteMilliseconds,5:N2}x)"));
        }
    }

    private static string BuildSessionId(int sessionIndex) =>
        string.Create(CultureInfo.InvariantCulture, $"session-{sessionIndex:D5}");

    private static string BuildSessionMarker(int sessionIndex) =>
        string.Create(CultureInfo.InvariantCulture, $"{SessionMarkerPrefix}{sessionIndex:D5}");

    private static string BuildChunkText(int sessionIndex, int chunkIndex)
    {
        var builder = new StringBuilder(BuildSessionMarker(sessionIndex));
        builder.Append(" chunk").Append(chunkIndex).Append(' ');

        for (var wordIndex = 0; wordIndex < WordsPerChunk; wordIndex++)
        {
            builder.Append(Vocabulary[((chunkIndex * 7) + (wordIndex * 13) + sessionIndex) % Vocabulary.Length]).Append(' ');
        }

        return builder.ToString();
    }

    /// <summary>
    /// A synthetic corpus of fixed-size sessions. Growing it adds sessions rather than enlarging
    /// them, so that per-session delete cost stays constant while total table size grows.
    /// </summary>
    private sealed record Corpus(int SessionCount, int ChunksPerSession)
    {
        public int ChunkCount => SessionCount * ChunksPerSession;

        public IEnumerable<ChunkRow> BuildChunks()
        {
            for (var chunkIndex = 0; chunkIndex < ChunkCount; chunkIndex++)
            {
                var owningSession = chunkIndex % SessionCount;

                yield return new ChunkRow(
                    chunkIndex + 1,
                    BuildSessionId(owningSession),
                    chunkIndex % KindCount,
                    chunkIndex,
                    BuildChunkText(owningSession, chunkIndex));
            }
        }
    }

    /// <summary>One synthetic chunk, shared by every candidate layout so the corpora are identical.</summary>
    private sealed record ChunkRow(long Id, string SessionId, int Kind, long Timestamp, string Text);

    /// <summary>The three candidate layouts measured against one corpus size.</summary>
    private sealed record StrategyComparison(
        StrategyMeasurement ContentOwning,
        StrategyMeasurement Contentless,
        StrategyMeasurement ExternalContent)
    {
        public IReadOnlyList<StrategyMeasurement> All => [ContentOwning, Contentless, ExternalContent];
    }

    /// <summary>One candidate layout and everything measured about it.</summary>
    private sealed record StrategyMeasurement(
        string Name,
        double PopulateMilliseconds,
        long DatabaseBytes,
        double TotalDeleteMilliseconds,
        double MeanDeleteMilliseconds,
        double MedianDeleteMilliseconds,
        double MaxDeleteMilliseconds)
    {
        public double DatabaseMegabytes => DatabaseBytes / 1024d / 1024d;
    }
}
