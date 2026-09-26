using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Configuration;
using SessionFinder.Infrastructure.FileSystem;
using SessionFinder.Infrastructure.Persistence;

namespace SessionFinder.Cli.Commands;

/// <summary>
/// Runs the background indexer in the foreground and prints what it does, until interrupted.
/// </summary>
/// <remarks>
/// <para>
/// This is the only command that keeps the engine running rather than asking it one question. It
/// starts with a full pass and then follows the watcher, and everything it prints comes from the
/// indexer itself: the notification, the plan the watermark chose, and the chunks that were
/// written. Read together they show the delay between a transcript being written and the index
/// holding it, which is the property this milestone is judged on and cannot be asserted from a
/// unit test against the real machine.
/// </para>
/// <para>
/// Interrupting it is part of what is being demonstrated, so the time between the interrupt and the
/// process being free to exit is measured and printed rather than assumed.
/// </para>
/// </remarks>
internal static class WatchCommand
{
    private const int ExitSuccess = 0;
    private const int ExitUsage = 64;
    private const int LabelWidth = 22;
    private const string TimestampFormat = "HH:mm:ss.fff";

    private static readonly TimeSpan ShutdownBudget = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs the command until the console is interrupted.
    /// </summary>
    /// <param name="arguments">Arguments after the verb; none are accepted.</param>
    /// <param name="cancellationToken">Signalled by the first interrupt, and what starts the drain.</param>
    /// <returns>A process exit code.</returns>
    public static async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != 0)
        {
            await Console.Error.WriteLineAsync("Usage: finder watch").ConfigureAwait(false);
            return ExitUsage;
        }

        using var host = CliHost.BuildIndexer(ShutdownBudget);
        var options = host.Services.GetRequiredService<IOptions<FinderOptions>>().Value;
        var watcher = host.Services.GetRequiredService<SessionFileWatcher>();
        var timeProvider = host.Services.GetRequiredService<TimeProvider>();

        WriteHeader(host.Services.GetRequiredService<SqliteIndexDatabase>(), watcher, options);

        var notices = new ChangeNoticeReporter(timeProvider, SettleFor(options));
        var drain = new DrainTimer(timeProvider);

        watcher.Changed += notices.OnFileChanged;

        using var registration = cancellationToken.Register(drain.Start);

        try
        {
            await host.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            drain.Start();
        }
        finally
        {
            watcher.Changed -= notices.OnFileChanged;
        }

        WriteDrainReport(drain);

        return ExitSuccess;
    }

    private static void WriteHeader(
        SqliteIndexDatabase database,
        SessionFileWatcher watcher,
        FinderOptions options)
    {
        WriteLine("Index", database.DatabasePath);
        WriteLine("Watching", watcher.ProjectsDirectory);
        WriteLine("Settle after", Invariant($"{options.ChangeSettleMilliseconds:N0} ms"));
        WriteLine("Index at the latest", Invariant($"{options.ChangeMaximumWaitMilliseconds:N0} ms"));
        WriteLine("Shutdown budget", Invariant($"{ShutdownBudget.TotalSeconds:N0} s"));
        Console.WriteLine();
    }

    private static void WriteDrainReport(DrainTimer drain)
    {
        Console.WriteLine();
        WriteLine(
            "Drained in",
            drain.Elapsed is { } elapsed ? Invariant($"{elapsed.TotalSeconds:N2} s") : "-");
    }

    private static TimeSpan SettleFor(FinderOptions options) =>
        TimeSpan.FromMilliseconds(options.ChangeSettleMilliseconds);

    private static void WriteLine(string label, string value) =>
        Console.WriteLine($"{label,-LabelWidth}: {value}");

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);

    /// <summary>
    /// Measures the time between the interrupt and the host being finished with the indexer, which
    /// is the number the shutdown budget is claimed against.
    /// </summary>
    /// <remarks>
    /// The interrupt can be observed twice — once by the token registration and once by the host's
    /// own console lifetime — so only the first observation is kept.
    /// </remarks>
    private sealed class DrainTimer(TimeProvider timeProvider)
    {
        private long _startedAt;

        /// <summary>How long the drain took, or <see langword="null"/> when it never started.</summary>
        public TimeSpan? Elapsed =>
            _startedAt == 0 ? null : timeProvider.GetElapsedTime(_startedAt);

        /// <summary>Records the moment the interrupt was seen, the first time it is seen.</summary>
        public void Start()
        {
            if (Interlocked.CompareExchange(ref _startedAt, timeProvider.GetTimestamp(), 0) != 0)
            {
                return;
            }

            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{timeProvider.GetLocalNow().ToString(TimestampFormat, CultureInfo.InvariantCulture)} stopping: finishing the transcript in hand."));
        }
    }
}
