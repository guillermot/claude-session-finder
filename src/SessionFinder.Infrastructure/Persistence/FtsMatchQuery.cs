namespace SessionFinder.Infrastructure.Persistence;

/// <summary>
/// One search translated into the FTS5 dialect: the individual terms a session has to contain, and
/// the single expression that collects every chunk matching any of them.
/// </summary>
/// <remarks>
/// Both forms are needed because the two halves of a search ask different questions. Qualifying a
/// session asks "does this session mention every term, anywhere in it", which is answered one term
/// at a time; scoring it asks "how strongly does each of its chunks match", which is answered by a
/// single expression so that a chunk mentioning two terms at once outranks a chunk mentioning one.
/// </remarks>
/// <param name="Terms">
/// Each searchable word as a quoted phrase with the prefix operator, for example
/// <c>"half-open"*</c>. The quoting is what stops a hyphen being read as an operator.
/// </param>
/// <param name="AnyTermExpression">The terms joined by <c>OR</c>, ready for <c>MATCH</c>.</param>
/// <param name="TermsJson">
/// The terms as a JSON array, which is how a statement receives a list SQLite can iterate over.
/// </param>
public sealed record FtsMatchQuery(IReadOnlyList<string> Terms, string AnyTermExpression, string TermsJson)
{
    /// <summary>The query built from input holding nothing searchable.</summary>
    public static FtsMatchQuery Empty { get; } = new([], string.Empty, "[]");

    /// <summary>
    /// Whether the input held nothing worth searching for, in which case no match must be run.
    /// </summary>
    public bool IsEmpty => Terms.Count == 0;
}
