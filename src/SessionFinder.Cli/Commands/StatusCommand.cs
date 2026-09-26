using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.IndexStatus;

namespace SessionFinder.Cli.Commands;

/// <summary>
/// Prints the state of the index.
/// </summary>
internal static class StatusCommand
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 64;
    private const int LabelWidth = 22;
    private const string AbsentMarker = "-";

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">Arguments after the verb; none are accepted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A process exit code.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != 0)
        {
            await Console.Error.WriteLineAsync("Usage: finder status").ConfigureAwait(false);
            return ExitUsage;
        }

        using var host = CliHost.Build();
        var handler = host.Services.GetRequiredService<IGetIndexStatusHandler>();

        var result = await handler
            .HandleAsync(GetIndexStatusQuery.Instance, cancellationToken)
            .ConfigureAwait(false);

        WriteReport(result.Index);

        return ExitSuccess;
    }

    private static void WriteReport(IndexStatistics index)
    {
        WriteLine("Database", index.DatabasePath);

        if (!index.DatabaseExists)
        {
            WriteLine("State", "not built yet - run 'finder reindex'");
            return;
        }

        WriteLine("Size", Invariant($"{index.DatabaseSizeBytes:N0} bytes"));
        WriteLine("Sessions", index.SessionCount.ToString("N0", CultureInfo.InvariantCulture));
        WriteLine("Chunks", index.ChunkCount.ToString("N0", CultureInfo.InvariantCulture));

        WriteChunkCounts(index);

        WriteLine("Sessions with errors", index.SessionsWithParseErrors.ToString("N0", CultureInfo.InvariantCulture));
        WriteLine("Folder unknown", index.SessionsWithUnknownFolder.ToString("N0", CultureInfo.InvariantCulture));
        WriteLine("Newest activity", FormatTimestamp(index.NewestActivity));
        WriteLine("Last reconcile", FormatTimestamp(index.LastReconcileAt));
    }

    private static void WriteChunkCounts(IndexStatistics index)
    {
        foreach (var (kind, count) in index.ChunksByKind.OrderBy(entry => entry.Key))
        {
            Console.WriteLine(Invariant($"{string.Empty,LabelWidth}  {kind,-16} {count,9:N0}"));
        }
    }

    private static string FormatTimestamp(DateTimeOffset? value) =>
        value is { } present
            ? present.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : AbsentMarker;

    private static void WriteLine(string label, string value) =>
        Console.WriteLine($"{label,-LabelWidth}: {value}");

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);
}
