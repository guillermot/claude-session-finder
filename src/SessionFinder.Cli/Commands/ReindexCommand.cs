using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using SessionFinder.Core.Features.ReconcileIndex;
using SessionFinder.Infrastructure.Persistence;

namespace SessionFinder.Cli.Commands;

/// <summary>
/// Brings the index in step with the transcripts on disk and reports what it did.
/// </summary>
/// <remarks>
/// This is how a full pass over the corpus is verified without a user interface: counts, bytes,
/// wall time and the resulting size of the index. Transcript text is never printed.
/// </remarks>
internal static class ReindexCommand
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 64;
    private const int LabelWidth = 22;
    private const string FullFlag = "--full";
    private const int MinimumProgressLineWidth = 1;

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="arguments">Arguments after the verb; an optional <c>--full</c>.</param>
    /// <param name="cancellationToken">Cancels between transcripts.</param>
    /// <returns>A process exit code.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!TryReadArguments(arguments, out var forceFullReparse))
        {
            await Console.Error.WriteLineAsync($"Usage: finder reindex [{FullFlag}]").ConfigureAwait(false);
            return ExitUsage;
        }

        using var host = CliHost.Build();
        var database = host.Services.GetRequiredService<SqliteIndexDatabase>();
        var handler = host.Services.GetRequiredService<IReconcileIndexHandler>();

        Console.WriteLine($"Index: {database.DatabasePath}");

        var command = new ReconcileIndexCommand
        {
            ForceFullReparse = forceFullReparse,
            Progress = CreateProgressReporter(),
        };

        var result = await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
        await database.CheckpointAsync(cancellationToken).ConfigureAwait(false);

        ClearProgressLine();
        WriteSummary(result, database.FileSizeBytes);

        return ExitSuccess;
    }

    private static bool TryReadArguments(IReadOnlyList<string> arguments, out bool forceFullReparse)
    {
        forceFullReparse = false;

        if (arguments.Count == 0)
        {
            return true;
        }

        forceFullReparse = arguments.Count == 1
            && string.Equals(arguments[0], FullFlag, StringComparison.OrdinalIgnoreCase);

        return forceFullReparse;
    }

    /// <summary>
    /// Reports progress on a single rewritten line, and only when a terminal is attached: a
    /// carriage return in redirected output produces an unreadable file.
    /// </summary>
    private static IProgress<ReconcileIndexProgress>? CreateProgressReporter()
    {
        if (Console.IsOutputRedirected)
        {
            return null;
        }

        return new ConsoleProgressReporter();
    }

    private static void WriteProgressLine(ReconcileIndexProgress progress)
    {
        var line = Invariant(
            $"  [{progress.Completed,4:N0}/{progress.Total,-4:N0}] {progress.Outcome,-22} {progress.SessionId}");

        Console.Write($"\r{line.PadRight(ProgressLineWidth)}");
    }

    private static void ClearProgressLine()
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        Console.Write($"\r{new string(' ', ProgressLineWidth)}\r");
    }

    /// <summary>
    /// Width the progress line is padded to. Clamped because a console can legitimately report a
    /// width of zero, and padding to a negative width throws.
    /// </summary>
    private static int ProgressLineWidth => Math.Max(MinimumProgressLineWidth, Console.WindowWidth - 1);

    private static void WriteSummary(ReconcileIndexResult result, long databaseSizeBytes)
    {
        WriteLine("Files discovered", result.FilesDiscovered.ToString("N0", CultureInfo.InvariantCulture));
        WriteLine("Sessions indexed", result.SessionsIndexed.ToString("N0", CultureInfo.InvariantCulture));
        WriteLine("Sessions skipped", result.SessionsSkipped.ToString("N0", CultureInfo.InvariantCulture));
        WriteLine("Sessions pruned", result.SessionsPruned.ToString("N0", CultureInfo.InvariantCulture));
        WriteLine("Chunks written", result.ChunksWritten.ToString("N0", CultureInfo.InvariantCulture));
        WriteLine("Transcript bytes", Invariant($"{result.BytesRead:N0}"));
        WriteLine("Sessions with errors", result.SessionsWithParseErrors.ToString("N0", CultureInfo.InvariantCulture));
        WriteLine("Files unreadable", result.FilesFailed.ToString("N0", CultureInfo.InvariantCulture));
        WriteLine("Index size", Invariant($"{databaseSizeBytes:N0} bytes"));
        WriteLine("Elapsed", Invariant($"{result.Elapsed.TotalSeconds:N2} s"));
        WriteLine("Throughput", FormatThroughput(result));
    }

    private static string FormatThroughput(ReconcileIndexResult result)
    {
        if (result.Elapsed.TotalSeconds <= 0 || result.BytesRead == 0)
        {
            return "-";
        }

        const double BytesPerMegabyte = 1024 * 1024;
        var megabytesPerSecond = result.BytesRead / BytesPerMegabyte / result.Elapsed.TotalSeconds;

        return Invariant($"{megabytesPerSecond:N1} MB/s");
    }

    private static void WriteLine(string label, string value) =>
        Console.WriteLine($"{label,-LabelWidth}: {value}");

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);

    /// <summary>
    /// Writes progress on the thread that reports it, rather than posting it elsewhere: the reports
    /// overwrite one line, so they have to stay in order.
    /// </summary>
    private sealed class ConsoleProgressReporter : IProgress<ReconcileIndexProgress>
    {
        public void Report(ReconcileIndexProgress value) => WriteProgressLine(value);
    }
}
