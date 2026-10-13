using SessionFinder.Core.Abstractions;

namespace SessionFinder.Core.Tests.Features.DailyRecap;

/// <summary>
/// Answers repository questions from tables a test fills in, and records what it was asked.
/// </summary>
internal sealed class StubGitActivityReader : IGitActivityReader
{
    private readonly Dictionary<string, string> _roots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<GitCommit>> _commits = new(StringComparer.Ordinal);

    public List<string> RootLookups { get; } = [];

    public List<string> LogRequests { get; } = [];

    public void HasRepository(string root, params string[] folders)
    {
        foreach (var folder in folders)
        {
            _roots[folder] = root;
        }
    }

    public void HasCommits(string root, params GitCommit[] commits) => _commits[root] = [.. commits];

    public Task<string?> FindRepositoryRootAsync(string folder, CancellationToken cancellationToken)
    {
        RootLookups.Add(folder);

        return Task.FromResult(_roots.TryGetValue(folder, out var root) ? root : null);
    }

    public Task<IReadOnlyList<GitCommit>> GetCommitsAsync(
        string repositoryRoot,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        LogRequests.Add(repositoryRoot);

        IReadOnlyList<GitCommit> found = _commits.TryGetValue(repositoryRoot, out var commits)
            ? [.. commits.Where(commit => commit.Timestamp >= from && commit.Timestamp < to)]
            : [];

        return Task.FromResult(found);
    }
}
