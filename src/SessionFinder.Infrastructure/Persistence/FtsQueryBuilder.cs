using System.Buffers;
using System.Text;
using System.Text.Json;

namespace SessionFinder.Infrastructure.Persistence;

/// <summary>
/// Translates free-form search box input into an FTS5 <c>MATCH</c> expression.
/// </summary>
/// <remarks>
/// <para>
/// The FTS5 query language treats characters such as <c>-</c>, <c>*</c>, <c>"</c>, <c>(</c>,
/// <c>^</c> and <c>:</c> as syntax, so passing raw user input to <c>MATCH</c> raises a
/// <c>SqliteException</c> rather than returning no rows. Every term is therefore emitted as a
/// quoted phrase with a trailing prefix operator.
/// </para>
/// <para>
/// Emitting <c>"term"*</c> instead of <c>term*</c> has a second, wanted effect: a hyphenated
/// token becomes a phrase whose parts must be adjacent, which is exactly the matching rule an
/// issue key needs, for free.
/// </para>
/// <para>
/// The terms are joined by <c>OR</c>, not by <c>AND</c>. Requiring every term inside one chunk
/// turned out to be a filter nobody wants: measured against the real corpus, five of twelve
/// plausible two-word questions returned nothing, because a conversation mentions its subject in
/// one message and the problem in another. Requiring all of them at the level of the session is
/// done by the statement that runs this query, not by the expression itself.
/// </para>
/// <para>
/// An empty result means "there is nothing searchable here": callers must skip the query and
/// show recent sessions instead of running a match that would return everything or throw.
/// </para>
/// </remarks>
public static class FtsQueryBuilder
{
    private const char PhraseDelimiter = '"';
    private const string EscapedPhraseDelimiter = "\"\"";
    private const string PrefixOperator = "*";
    private const string TermSeparator = " OR ";

    /// <summary>
    /// Builds a phrase-prefix FTS5 query from raw user input.
    /// </summary>
    /// <param name="userInput">Whatever the user typed, including nothing at all.</param>
    /// <returns>
    /// The translated query, or <see cref="FtsMatchQuery.Empty"/> when the input contains no
    /// searchable term.
    /// </returns>
    public static FtsMatchQuery Build(string? userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return FtsMatchQuery.Empty;
        }

        string[] terms =
        [
            .. userInput
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(IsSearchable)
                .Select(ToPhrasePrefix),
        ];

        return terms.Length == 0
            ? FtsMatchQuery.Empty
            : new FtsMatchQuery(terms, string.Join(TermSeparator, terms), ToJsonArray(terms));
    }

    /// <summary>
    /// Decides whether a whitespace-delimited term can produce a token once FTS5 has
    /// tokenised it. Terms made entirely of punctuation cannot, and are dropped.
    /// </summary>
    private static bool IsSearchable(string term) => term.Any(char.IsLetterOrDigit);

    /// <summary>
    /// Wraps a term in FTS5 phrase quotes and appends the prefix operator, doubling any
    /// embedded quote so it is read as literal text rather than as the end of the phrase.
    /// </summary>
    private static string ToPhrasePrefix(string term)
    {
        var escaped = term.Replace(PhraseDelimiter.ToString(), EscapedPhraseDelimiter, StringComparison.Ordinal);

        return string.Concat(PhraseDelimiter, escaped, PhraseDelimiter, PrefixOperator);
    }

    /// <summary>
    /// Renders the terms as a JSON array, written rather than serialised so the escaping is exact
    /// and the path stays free of reflection.
    /// </summary>
    private static string ToJsonArray(IReadOnlyList<string> terms)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();

            foreach (var term in terms)
            {
                writer.WriteStringValue(term);
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
