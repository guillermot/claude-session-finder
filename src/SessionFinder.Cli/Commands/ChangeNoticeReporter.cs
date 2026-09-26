using System.Collections.Concurrent;
using System.Globalization;
using SessionFinder.Core.Domain;
using SessionFinder.Infrastructure.FileSystem;

namespace SessionFinder.Cli.Commands;

/// <summary>
/// Prints the raw change notifications the watcher raises, so the wait between a transcript being
/// written and the index catching up is visible rather than inferred.
/// </summary>
/// <remarks>
/// <para>
/// A session being worked in produces a notification every few hundred milliseconds, and printing
/// every one of them would bury the indexing events this command exists to show. One line per
/// transcript per interval is enough to show that the file is still being written, which is the
/// only thing the raw notifications say.
/// </para>
/// <para>
/// Notifications arrive on the watcher's own threads, so the record of what was last printed is
/// held in a concurrent map rather than guarded by the console.
/// </para>
/// </remarks>
internal sealed class ChangeNoticeReporter(TimeProvider timeProvider, TimeSpan minimumInterval)
{
    private const string NoticeLabel = "changed";
    private const string TimestampFormat = "HH:mm:ss.fff";

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastPrinted =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Handles a change notification from the watcher.
    /// </summary>
    /// <param name="sender">The watcher that raised it.</param>
    /// <param name="e">The path that changed.</param>
    public void OnFileChanged(object? sender, SessionFileChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (!ShouldPrint(e.FilePath))
        {
            return;
        }

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{timeProvider.GetLocalNow().ToString(TimestampFormat, CultureInfo.InvariantCulture)} {NoticeLabel}: {Describe(e.FilePath)}"));
    }

    /// <summary>
    /// Whether this path has gone long enough without a printed notice to deserve another one.
    /// </summary>
    private bool ShouldPrint(string filePath)
    {
        var now = timeProvider.GetUtcNow();

        if (_lastPrinted.TryGetValue(filePath, out var printedAt) && now - printedAt < minimumInterval)
        {
            return false;
        }

        _lastPrinted[filePath] = now;

        return true;
    }

    /// <summary>
    /// Names the transcript by its session, falling back to the file name for anything that is not
    /// one.
    /// </summary>
    private static string Describe(string filePath) =>
        SessionId.TryParseFromFileName(filePath, out var sessionId)
            ? sessionId.ToString()
            : Path.GetFileName(filePath);
}
