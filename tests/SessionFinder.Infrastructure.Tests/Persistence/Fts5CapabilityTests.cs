using Microsoft.Data.Sqlite;
using SessionFinder.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace SessionFinder.Infrastructure.Tests.Persistence;

/// <summary>
/// Answers the capability questions the index schema depends on, against the SQLite build
/// that ships inside <c>Microsoft.Data.Sqlite</c>.
/// </summary>
public sealed class Fts5CapabilityTests : IDisposable
{
    private const string TokenizerOptions = "unicode61 remove_diacritics 2";
    private const string PrefixIndexSizes = "2 3 4";

    private readonly ITestOutputHelper _output;
    private readonly TemporaryDatabase _database;
    private readonly SqliteConnection _connection;

    public Fts5CapabilityTests(ITestOutputHelper output)
    {
        _output = output;
        _database = new TemporaryDatabase();
        _connection = _database.OpenWriter();
    }

    [Fact]
    public void CompileOptions_BundledSqlite_ExposeFts5()
    {
        var version = _connection.Scalar<string>("SELECT sqlite_version();");

        var fts5Enabled = _connection.Scalar<long>("SELECT sqlite_compileoption_used('ENABLE_FTS5');");

        _output.WriteLine($"SQLite version                 : {version}");
        _output.WriteLine($"ENABLE_FTS5                    : {fts5Enabled}");
        _output.WriteLine($"contentless_delete needs       : 3.43.0 or newer");
        fts5Enabled.Should().Be(1, "the whole index design assumes FTS5 is compiled into the bundled SQLite");
    }

    [Theory]
    [InlineData("deposito", "depósito")]
    [InlineData("depósito", "deposito")]
    [InlineData("ano", "año")]
    [InlineData("año", "ano")]
    public void Match_DiacriticFolding_MatchesRegardlessOfAccent(string indexedWord, string searchedWord)
    {
        CreateSimpleIndex();
        InsertDocument(1, $"the {indexedWord} appears once in this line");

        var matches = _connection.QueryStrings(
            "SELECT text FROM simple_fts WHERE simple_fts MATCH $query;",
            ("$query", FtsQueryBuilder.Build(searchedWord).AnyTermExpression));

        matches.Should().ContainSingle();
    }

    [Theory]
    [InlineData("de")]
    [InlineData("dep")]
    [InlineData("depo")]
    [InlineData("deposi")]
    public void Match_PrefixIndex_MatchesPartialToken(string typedSoFar)
    {
        CreateSimpleIndex();
        InsertDocument(1, "reconciling the deposito for the wallet");

        var matches = _connection.QueryStrings(
            "SELECT text FROM simple_fts WHERE simple_fts MATCH $query;",
            ("$query", FtsQueryBuilder.Build(typedSoFar).AnyTermExpression));

        matches.Should().ContainSingle();
    }

    [Fact]
    public void Bm25_TitleColumnWeighted_RanksTitleMatchAboveRepeatedBodyMatches()
    {
        const string RankByWeightedTitle =
            """
            SELECT name
            FROM weighted_fts
            WHERE weighted_fts MATCH $query
            ORDER BY bm25(weighted_fts, 10.0, 1.0);
            """;
        CreateWeightedIndex();
        InsertWeightedDocument("title-match", title: "deposit reconciliation", body: "unrelated notes about the pipeline");
        InsertWeightedDocument("body-match", title: "unrelated heading", body: "deposit deposit deposit deposit deposit");

        var ranked = _connection.QueryStrings(RankByWeightedTitle, ("$query", FtsQueryBuilder.Build("deposit").AnyTermExpression));

        _output.WriteLine($"bm25(title 10.0, body 1.0) order : {string.Join(" > ", ranked)}");
        ranked.Should().Equal("title-match", "body-match");
    }

    [Fact]
    public void Bm25_BodyColumnWeighted_RanksRepeatedBodyMatchesFirst()
    {
        const string RankByWeightedBody =
            """
            SELECT name
            FROM weighted_fts
            WHERE weighted_fts MATCH $query
            ORDER BY bm25(weighted_fts, 1.0, 10.0);
            """;
        CreateWeightedIndex();
        InsertWeightedDocument("title-match", title: "deposit reconciliation", body: "unrelated notes about the pipeline");
        InsertWeightedDocument("body-match", title: "unrelated heading", body: "deposit deposit deposit deposit deposit");

        var ranked = _connection.QueryStrings(RankByWeightedBody, ("$query", FtsQueryBuilder.Build("deposit").AnyTermExpression));

        _output.WriteLine($"bm25(title 1.0, body 10.0) order : {string.Join(" > ", ranked)}");
        ranked.Should().Equal("body-match", "title-match");
    }

    public void Dispose()
    {
        _connection.Dispose();
        _database.Dispose();
    }

    private void CreateSimpleIndex()
    {
        _connection.Execute(
            $"""
             CREATE VIRTUAL TABLE simple_fts USING fts5(
                 text,
                 tokenize='{TokenizerOptions}',
                 prefix='{PrefixIndexSizes}'
             );
             """);
    }

    private void CreateWeightedIndex()
    {
        _connection.Execute(
            $"""
             CREATE VIRTUAL TABLE weighted_fts USING fts5(
                 title,
                 body,
                 name UNINDEXED,
                 tokenize='{TokenizerOptions}',
                 prefix='{PrefixIndexSizes}'
             );
             """);
    }

    private void InsertDocument(long rowId, string text) =>
        _connection.Execute(
            "INSERT INTO simple_fts(rowid, text) VALUES ($rowid, $text);",
            ("$rowid", rowId),
            ("$text", text));

    private void InsertWeightedDocument(string name, string title, string body) =>
        _connection.Execute(
            "INSERT INTO weighted_fts(title, body, name) VALUES ($title, $body, $name);",
            ("$title", title),
            ("$body", body),
            ("$name", name));
}
