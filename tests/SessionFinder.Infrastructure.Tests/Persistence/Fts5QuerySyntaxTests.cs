using Microsoft.Data.Sqlite;
using SessionFinder.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace SessionFinder.Infrastructure.Tests.Persistence;

/// <summary>
/// Pins down how raw search-box input has to be translated before it reaches <c>MATCH</c>:
/// a hyphenated token is FTS5 syntax, not a word, and arbitrary punctuation must degrade to
/// "no results" rather than to an exception surfacing in the search box.
/// </summary>
public sealed class Fts5QuerySyntaxTests : IDisposable
{
    private const string TokenizerOptions = "unicode61 remove_diacritics 2";
    private const string PrefixIndexSizes = "2 3 4";
    private const string MatchByQuery = "SELECT name FROM notes_fts WHERE notes_fts MATCH $query;";

    private const string HyphenatedToken = "ABC-123";
    private const string LongerHyphenatedToken = "ABC-1234";

    private readonly ITestOutputHelper _output;
    private readonly TemporaryDatabase _database;
    private readonly SqliteConnection _connection;

    public Fts5QuerySyntaxTests(ITestOutputHelper output)
    {
        _output = output;
        _database = new TemporaryDatabase();
        _connection = _database.OpenWriter();

        _connection.Execute(
            $"""
             CREATE VIRTUAL TABLE notes_fts USING fts5(
                 text,
                 name UNINDEXED,
                 tokenize='{TokenizerOptions}',
                 prefix='{PrefixIndexSizes}'
             );
             """);

        InsertNote("exact", $"fix the {HyphenatedToken} deposit mismatch");
        InsertNote("longer", $"{LongerHyphenatedToken} follow up on the same batch");
        InsertNote("far-apart", "wal reconciliation notes, and much later the number 920 shows up");
    }

    [Fact]
    public void Match_HyphenatedTokenUnquoted_ThrowsBecauseTheHyphenIsSyntax()
    {
        var runUnquotedMatch = () => _connection.QueryStrings(MatchByQuery, ("$query", HyphenatedToken));

        var thrown = runUnquotedMatch.Should().Throw<SqliteException>().Which;

        _output.WriteLine($"unquoted MATCH '{HyphenatedToken}' : {thrown.GetType().Name}: {thrown.Message}");
    }

    [Fact]
    public void Match_HyphenatedTokenAsPhrasePrefix_MatchesTheExactToken()
    {
        var query = FtsQueryBuilder.Build(HyphenatedToken).AnyTermExpression;

        var matches = _connection.QueryStrings(MatchByQuery, ("$query", query));

        _output.WriteLine($"built query : {query}");
        matches.Should().Contain("exact");
    }

    [Fact]
    public void Match_HyphenatedTokenAsPhrasePrefix_MatchesALongerToken()
    {
        var query = FtsQueryBuilder.Build(HyphenatedToken).AnyTermExpression;

        var matches = _connection.QueryStrings(MatchByQuery, ("$query", query));

        matches.Should().Contain("longer");
    }

    [Fact]
    public void Match_HyphenatedTokenAsPhrasePrefix_RequiresAdjacency()
    {
        var query = FtsQueryBuilder.Build(HyphenatedToken).AnyTermExpression;

        var matches = _connection.QueryStrings(MatchByQuery, ("$query", query));

        matches.Should().NotContain("far-apart");
    }

    [Theory]
    [InlineData("-")]
    [InlineData("\"")]
    [InlineData("*")]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData("^")]
    [InlineData(":")]
    [InlineData("NEAR(")]
    [InlineData("   ")]
    [InlineData("\"unterminated")]
    [InlineData("C:\\git\\Project\\src")]
    [InlineData("c:\\git\\some folder\\file.jsonl")]
    public void Match_JunkInput_ReturnsNoRowsWithoutThrowing(string userInput)
    {
        var query = FtsQueryBuilder.Build(userInput).AnyTermExpression;

        List<string> matches = query.Length == 0 ? [] : _connection.QueryStrings(MatchByQuery, ("$query", query));

        _output.WriteLine($"input {userInput,-32} -> query {(query.Length == 0 ? "<skipped>" : query),-42} -> {matches.Count} rows");
        matches.Should().BeEmpty();
    }

    [Fact]
    public void Build_EmptyInput_SignalsThatNoQueryShouldRun()
    {
        var query = FtsQueryBuilder.Build(string.Empty);

        query.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Build_MultipleWords_MatchesAnyOfThemSoTheConjunctionCanBeAppliedPerSession()
    {
        var query = FtsQueryBuilder.Build($"  {HyphenatedToken}   deposit  ");

        query.AnyTermExpression.Should().Be($"\"{HyphenatedToken}\"* OR \"deposit\"*");
    }

    [Fact]
    public void Build_MultipleWords_KeepsEveryTermSeparatelyAvailable()
    {
        var query = FtsQueryBuilder.Build($"{HyphenatedToken} deposit");

        query.Terms.Should().Equal($"\"{HyphenatedToken}\"*", "\"deposit\"*");
    }

    [Fact]
    public void Build_MultipleWords_RendersTheTermsAsAJsonArray()
    {
        var query = FtsQueryBuilder.Build("alpha beta");

        query.TermsJson.Should().Be("[\"\\u0022alpha\\u0022*\",\"\\u0022beta\\u0022*\"]");
    }

    [Fact]
    public void Build_EmbeddedQuote_IsDoubledSoThePhraseStaysIntact()
    {
        var query = FtsQueryBuilder.Build("say\"hello");

        query.AnyTermExpression.Should().Be("\"say\"\"hello\"*");
    }

    public void Dispose()
    {
        _connection.Dispose();
        _database.Dispose();
    }

    private void InsertNote(string name, string text) =>
        _connection.Execute(
            "INSERT INTO notes_fts(text, name) VALUES ($text, $name);",
            ("$text", text),
            ("$name", name));
}
