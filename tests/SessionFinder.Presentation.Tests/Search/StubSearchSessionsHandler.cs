using SessionFinder.Core.Features.Search;

namespace SessionFinder.Presentation.Tests.Search;

/// <summary>
/// Answers whatever the test decided, and records what it was asked.
/// </summary>
internal sealed class StubSearchSessionsHandler : ISearchSessionsHandler
{
    private readonly List<string?> _queries = [];

    private Func<SearchSessionsQuery, CancellationToken, Task<SearchSessionsResult>> _answer =
        (_, _) => Task.FromResult(SearchSessionsResult.Empty);

    public IReadOnlyList<string?> Queries => _queries;

    public void Answers(SearchSessionsResult result) => _answer = (_, _) => Task.FromResult(result);

    public void Throws(Exception exception) => _answer = (_, _) => Task.FromException<SearchSessionsResult>(exception);

    public void Answers(Func<SearchSessionsQuery, CancellationToken, Task<SearchSessionsResult>> answer) =>
        _answer = answer;

    public Task<SearchSessionsResult> HandleAsync(SearchSessionsQuery query, CancellationToken cancellationToken)
    {
        _queries.Add(query.Text);

        return _answer(query, cancellationToken);
    }
}
