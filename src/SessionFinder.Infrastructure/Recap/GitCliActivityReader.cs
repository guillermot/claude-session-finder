using System.Globalization;
using Microsoft.Extensions.Logging;
using SessionFinder.Core.Abstractions;
using SessionFinder.Infrastructure.Processes;

namespace SessionFinder.Infrastructure.Recap;

/// <summary>
/// Answers the recap's questions about repositories by running the <c>git</c> command line.
/// </summary>
/// <remarks>
/// <para>
/// The command line rather than a library, because it is what the user already has, configured the
/// way they configured it: the same author identity, the same worktrees, the same safe-directory
/// rules. A library would need all of that rediscovered and would still disagree at the edges.
/// </para>
/// <para>
/// The author is the repository's own <c>user.email</c>, matched as a fixed string. A person with
/// a work address in one repository and a personal one in another gets the right commits in both,
/// and an address full of regular-expression characters matches itself.
/// </para>
/// <para>
/// <c>--since</c> and <c>--until</c> filter on the committer date, so a commit authored on the day
/// and rebased later can fall outside them, and one rebased onto the day can fall inside. The
/// author date is checked again here, which keeps the second kind out; the first is accepted as
/// the price of letting git do the filtering.
/// </para>
/// </remarks>
public sealed class GitCliActivityReader(IProcessRunner runner, ILogger<GitCliActivityReader> logger) : IGitActivityReader
{
    private const string GitExecutable = "git";
    private const char FieldSeparator = '\u001f';
    private const string LogFormat = "--pretty=format:%h%x1f%aI%x1f%s";
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);

    private static readonly IReadOnlyDictionary<string, string> QuietEnvironment = new Dictionary<string, string>
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_OPTIONAL_LOCKS"] = "0",
        ["LC_ALL"] = "C",
    };

    /// <inheritdoc />
    public async Task<string?> FindRepositoryRootAsync(string folder, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        if (!Directory.Exists(folder))
        {
            return null;
        }

        var output = await RunAsync(folder, ["rev-parse", "--show-toplevel"], cancellationToken).ConfigureAwait(false);
        var root = output?.Trim();

        return string.IsNullOrEmpty(root) ? null : Path.GetFullPath(root);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitCommit>> GetCommitsAsync(
        string repositoryRoot,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var author = await ReadAuthorAsync(repositoryRoot, cancellationToken).ConfigureAwait(false);

        if (author is null)
        {
            return [];
        }

        string[] arguments =
        [
            "log",
            "--all",
            "--no-merges",
            "--fixed-strings",
            $"--author={author}",
            $"--since={FormatInstant(from)}",
            $"--until={FormatInstant(to)}",
            LogFormat,
        ];

        var output = await RunAsync(repositoryRoot, arguments, cancellationToken).ConfigureAwait(false);

        return output is null ? [] : ParseLog(output, from, to);
    }

    /// <summary>
    /// Reads one commit per line in the format the log was asked for. A line that does not have all
    /// three fields — a subject cannot contain the separator, but a broken pipe can cut a line — is
    /// skipped rather than guessed at.
    /// </summary>
    /// <param name="output">The log output.</param>
    /// <param name="from">Start of the window, inclusive, compared against the author date.</param>
    /// <param name="to">End of the window, exclusive.</param>
    /// <returns>The commits, oldest first.</returns>
    public static IReadOnlyList<GitCommit> ParseLog(string output, DateTimeOffset from, DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(output);

        var commits = new List<GitCommit>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = line.Split(FieldSeparator, 3);

            if (fields.Length != 3
                || !DateTimeOffset.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var authored)
                || authored < from
                || authored >= to)
            {
                continue;
            }

            commits.Add(new GitCommit(fields[0], fields[2], authored));
        }

        return [.. commits.OrderBy(commit => commit.Timestamp)];
    }

    private async Task<string?> ReadAuthorAsync(string repositoryRoot, CancellationToken cancellationToken)
    {
        foreach (var key in (string[])["user.email", "user.name"])
        {
            var value = (await RunAsync(repositoryRoot, ["config", key], cancellationToken).ConfigureAwait(false))?.Trim();

            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Runs one git command in a folder. Any answer other than a clean exit is an absence: the
    /// recap is built from what git could say, never held up by what it could not.
    /// </summary>
    private async Task<string?> RunAsync(string folder, string[] arguments, CancellationToken cancellationToken)
    {
        var request = new ProcessRequest
        {
            FileName = GitExecutable,
            Arguments = ["-C", folder, .. arguments],
            Environment = QuietEnvironment,
            Timeout = CommandTimeout,
        };

        var outcome = await runner.RunAsync(request, cancellationToken).ConfigureAwait(false);

        if (outcome.Succeeded)
        {
            return outcome.StandardOutput;
        }

        RecapAdaptersLog.GitDidNotAnswer(logger, arguments[0], folder, outcome.ExitCode, outcome.TimedOut, outcome.StartError);

        return null;
    }

    private static string FormatInstant(DateTimeOffset instant) =>
        instant.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
