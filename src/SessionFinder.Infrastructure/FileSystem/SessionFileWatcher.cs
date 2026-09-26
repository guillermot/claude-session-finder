using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Configuration;

namespace SessionFinder.Infrastructure.FileSystem;

/// <summary>
/// Reports changes to the transcript files under the Claude projects directory.
/// </summary>
/// <remarks>
/// <para>
/// Every kind of change reports the same thing: a path worth looking at again. Creation, writing,
/// renaming and deletion all end in the same place, because the notification is only a hint — by
/// the time it is acted on the file may have grown again, or gone.
/// </para>
/// <para>
/// A rename reports both paths, because a transcript renamed away has to be noticed as missing at
/// its old location just as much as it has to be found at its new one.
/// </para>
/// <para>
/// The operating system buffer can overflow while a session is being written quickly, and the
/// notifications lost that way are lost for good. That is reported separately rather than ignored:
/// the answer to it is a full pass, not a longer buffer.
/// </para>
/// </remarks>
public sealed class SessionFileWatcher : IDisposable
{
    private const string TranscriptSearchPattern = "*.jsonl";
    private const int InternalBufferSize = 64 * 1024;

    private readonly ILogger<SessionFileWatcher> _logger;

    private FileSystemWatcher? _watcher;
    private bool _disposed;

    /// <summary>
    /// Resolves the directory to watch from configuration.
    /// </summary>
    /// <param name="options">The bound settings; a blank configuration directory selects the default.</param>
    /// <param name="logger">Receives the notifications that are dropped rather than raised.</param>
    public SessionFileWatcher(IOptions<FinderOptions> options, ILogger<SessionFileWatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        ProjectsDirectory = Path.GetFullPath(Path.Combine(
            ClaudeConfigurationDirectory.Resolve(options.Value.ClaudeConfigDirectory),
            ClaudeConfigurationDirectory.ProjectsFolderName));
    }

    /// <summary>Raised for every path worth examining again.</summary>
    public event EventHandler<SessionFileChangedEventArgs>? Changed;

    /// <summary>
    /// Raised when notifications were dropped, so the only honest recovery is a full pass.
    /// </summary>
    public event EventHandler? ChangesLost;

    /// <summary>The directory being watched, whether or not it exists.</summary>
    public string ProjectsDirectory { get; }

    /// <summary>
    /// Starts watching, or reports that there is nothing to watch.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the projects directory does not exist, in which case nothing is
    /// watched and the caller still has a working index, only not a live one.
    /// </returns>
    public bool Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_watcher is not null)
        {
            return true;
        }

        if (!Directory.Exists(ProjectsDirectory))
        {
            SessionFileWatcherLog.NothingToWatch(_logger, ProjectsDirectory);
            return false;
        }

        _watcher = CreateWatcher();
        SessionFileWatcherLog.WatchStarted(_logger, ProjectsDirectory);

        return true;
    }

    /// <summary>Stops watching and releases the operating system handle.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher?.Dispose();
        _watcher = null;
    }

    private FileSystemWatcher CreateWatcher()
    {
        var watcher = new FileSystemWatcher(ProjectsDirectory, TranscriptSearchPattern)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = InternalBufferSize,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };

        watcher.Created += OnFileSystemChange;
        watcher.Changed += OnFileSystemChange;
        watcher.Deleted += OnFileSystemChange;
        watcher.Renamed += OnFileRenamed;
        watcher.Error += OnError;
        watcher.EnableRaisingEvents = true;

        return watcher;
    }

    private void OnFileSystemChange(object sender, FileSystemEventArgs e) => Raise(e.FullPath);

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        Raise(e.OldFullPath);
        Raise(e.FullPath);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        SessionFileWatcherLog.NotificationsLost(_logger, e.GetException(), ProjectsDirectory);
        ChangesLost?.Invoke(this, EventArgs.Empty);
    }

    private void Raise(string filePath) => Changed?.Invoke(this, new SessionFileChangedEventArgs(filePath));
}
