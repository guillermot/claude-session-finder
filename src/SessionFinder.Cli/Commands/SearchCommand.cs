using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.Search;
using SessionFinder.Infrastructure.Persistence;

namespace SessionFinder.Cli.Commands;

/// <summary>
/// Runs one search against the index and prints the ranked results.
/// </summary>
/// <remarks>
/// This is the head that makes ranking arguable. The plain output answers "did it find the right
/// session"; <c>--verbose</c> answers "why is that one first", which is the only way to tune the
/// weights against a real corpus rather than against a guess.
/// </remarks>
internal static class SearchCommand
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 64;
    private const int LabelWidth = 9;
    private const int RowIndent = 13;
    private const string VerboseFlag = "--verbose";
    private const string VerboseShortFlag = "-v";
    private const string TakeFlag = "--take";
    private const string UsageText = "Usage: finder search [--verbose] [--take <n>] <query>";
    private const string AbsentMarker = "-";
    private const string EmptyQueryNote = "(nothing searchable - showing the most recent sessions)";

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">Arguments after the verb: optional flags, then the query words.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>A process exit code.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TryReadArguments(arguments, out var options))
        {
            await Console.Error.WriteLineAsync(UsageText).ConfigureAwait(false);
            return ExitUsage;
        }

        using var host = CliHost.Build();
        var database = host.Services.GetRequiredService<SqliteIndexDatabase>();
        var handler = host.Services.GetRequiredService<ISearchSessionsHandler>();

        var query = new SearchSessionsQuery { Text = options.Query, MaxResults = options.Take };

        var stopwatch = Stopwatch.StartNew();
        var result = await handler.HandleAsync(query, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        WriteReport(database.DatabasePath, options, result, stopwatch.Elapsed);

        return ExitSuccess;
    }

    private static bool TryReadArguments(IReadOnlyList<string> arguments, out SearchArguments options)
    {
        options = new SearchArguments { Query = string.Empty };
        var words = new List<string>();

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            if (IsFlag(argument, VerboseFlag, VerboseShortFlag))
            {
                options = options with { Verbose = true };
                continue;
            }

            if (!IsFlag(argument, TakeFlag))
            {
                words.Add(argument);
                continue;
            }

            if (!TryReadTake(arguments, ref index, out var take))
            {
                return false;
            }

            options = options with { Take = take };
        }

        options = options with { Query = string.Join(' ', words) };

        return true;
    }

    private static bool IsFlag(string argument, string name, string? alias = null) =>
        string.Equals(argument, name, StringComparison.OrdinalIgnoreCase)
        || (alias is not null && string.Equals(argument, alias, StringComparison.OrdinalIgnoreCase));

    private static bool TryReadTake(IReadOnlyList<string> arguments, ref int index, out int take)
    {
        take = 0;
        index++;

        return index < arguments.Count
            && int.TryParse(arguments[index], CultureInfo.InvariantCulture, out take)
            && take > 0;
    }

    private static void WriteReport(
        string databasePath,
        SearchArguments options,
        SearchSessionsResult result,
        TimeSpan elapsed)
    {
        WriteLine("Index", databasePath);
        WriteLine("Query", result.IsRecentFallback ? EmptyQueryNote : options.Query);
        WriteLine("Found", FormatCounters(result, elapsed));
        Console.WriteLine();

        if (result.Hits.Count == 0)
        {
            Console.WriteLine("  no sessions matched.");
            return;
        }

        var rank = 1;

        foreach (var hit in result.Hits)
        {
            WriteHit(rank++, hit, options.Verbose);
        }
    }

    private static string FormatCounters(SearchSessionsResult result, TimeSpan elapsed) => Invariant(
        $"{result.Hits.Count} shown of {result.CandidatesConsidered} candidate(s) in {elapsed.TotalMilliseconds:N1} ms");

    private static void WriteHit(int rank, SessionHit hit, bool verbose)
    {
        var session = hit.Session;

        Console.WriteLine(Invariant($"{rank,4}  {hit.Score,8:N2}  {session.Title.Text}"));
        Console.WriteLine(Indent(Invariant(
            $"{FormatTimestamp(session.LastActivity)}  ·  {FormatFolder(session.Folder)}")));

        if (session.Snippet is { } snippet)
        {
            Console.WriteLine(Indent(Flatten(snippet)));
        }

        if (verbose)
        {
            WriteDiagnostics(hit);
        }

        Console.WriteLine();
    }

    private static void WriteDiagnostics(SessionHit hit)
    {
        Console.WriteLine(Indent(Invariant(
            $"lexical {hit.LexicalScore:N3}  ×  recency {hit.RecencyMultiplier:N3}  ·  {FormatMatchedKinds(hit)}")));

        Console.WriteLine(Indent(FormatProvenance(hit.Session)));
        Console.WriteLine(Indent($"{hit.Session.SessionId}  ·  {hit.Session.FilePath}"));
    }

    private static string FormatProvenance(SessionSummary session)
    {
        var branch = session.GitBranch ?? AbsentMarker;

        return Invariant(
            $"title [{session.Title.Source}] · folder [{session.Folder.Source}] · branch {branch} · {session.MessageCount:N0} msg");
    }

    private static string FormatMatchedKinds(SessionHit hit)
    {
        if (hit.MatchedKinds.Count == 0)
        {
            return "no matched chunks";
        }

        var kinds = string.Join(", ", hit.MatchedKinds.Select(kind => kind.ToString()));

        return Invariant($"{kinds} ({hit.MatchedChunkCount} chunk(s))");
    }

    private static string FormatFolder(WorkingFolder folder) =>
        folder.IsKnown ? folder.Display : "folder unknown";

    private static string FormatTimestamp(DateTimeOffset? value) =>
        value is { } present
            ? present.ToUniversalTime().ToString("yyyy-MM-dd HH:mm'Z'", CultureInfo.InvariantCulture)
            : AbsentMarker;

    /// <summary>
    /// Collapses the line breaks a snippet inherits from a pasted prompt, so one result stays one
    /// line and the ranked list stays readable.
    /// </summary>
    private static string Flatten(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Indent(string text) => new string(' ', RowIndent) + text;

    private static void WriteLine(string label, string value) =>
        Console.WriteLine($"{label,-LabelWidth}: {value}");

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);

    /// <summary>
    /// What the command line asked for, once flags and query words have been separated.
    /// </summary>
    private readonly record struct SearchArguments
    {
        public string Query { get; init; }

        public int? Take { get; init; }

        public bool Verbose { get; init; }
    }
}
