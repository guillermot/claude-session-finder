using System.IO;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using SessionFinder.Core;
using SessionFinder.Infrastructure;
using SessionFinder.Infrastructure.FileSystem;
using SessionFinder.Presentation;

namespace SessionFinder.Wpf.Composition;

/// <summary>
/// Builds the generic host that owns every service in the Windows head, including the background
/// indexer, which runs in this process rather than beside it.
/// </summary>
/// <remarks>
/// <para>
/// The indexer is in-process because the two halves already share a database file that permits a
/// single writer. One process makes that a fact about the object graph instead of an agreement
/// between two executables, and it removes the second thing a user would otherwise have to arrange
/// to start.
/// </para>
/// <para>
/// Settings live next to the user rather than next to the executable, and are optional: the tool
/// runs with no settings file at all, and a file that appears later is picked up without a restart.
/// </para>
/// <para>
/// The content root is pinned to the directory the executable is in. A host left to its default
/// takes the working directory of whatever started it, and a tool launched from a shortcut, from
/// the run dialog or at logon has no meaningful working directory at all.
/// </para>
/// </remarks>
internal static class HostFactory
{
    private const string EventTimestampFormat = "HH:mm:ss.fff ";
    private const string LogFileName = "finder-.log";
    private const string LogLevelDefaultKey = "Logging:LogLevel:Default";
    private const string LoggingSectionName = "Logging";
    private const int LogFileSizeLimitBytes = 4 * 1024 * 1024;
    private const int LogFilesKept = 7;

    /// <summary>
    /// Composes the host: Core handlers, Infrastructure adapters, the background indexer, the view
    /// models and the Windows adapters.
    /// </summary>
    /// <param name="uiDispatcher">The dispatcher of the thread that will run the message loop.</param>
    /// <param name="shutdownTimeout">How long the host waits for the indexer to stop.</param>
    /// <returns>A host the caller must dispose.</returns>
    public static IHost Create(Dispatcher uiDispatcher, TimeSpan shutdownTimeout)
    {
        ArgumentNullException.ThrowIfNull(uiDispatcher);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Configuration.AddInMemoryCollection(DefaultLogLevel);
        builder.Configuration.AddJsonFile(ApplicationPaths.SettingsFile, optional: true, reloadOnChange: true);

        ConfigureLogging(builder.Logging, builder.Configuration);

        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = shutdownTimeout);

        builder.Services.AddSessionFinderCore(builder.Configuration);
        builder.Services.AddSessionFinderInfrastructure(builder.Configuration);
        builder.Services.AddSessionFinderIndexer();
        builder.Services.AddSessionFinderPresentation(builder.Configuration);
        builder.Services.AddWindowsShell(uiDispatcher);

        return builder.Build();
    }

    /// <summary>
    /// Sends the account of what happened to a rolling file, and to the console for anyone who
    /// started the executable from a terminal.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file provider is the one third-party component in the logging setup, and it is a
    /// provider rather than a framework: every line of this application logs through
    /// <see cref="ILogger"/> and nothing names it. The runtime ships no file provider, and a rolling
    /// file written by hand is a hundred lines of concurrency and file-locking that this
    /// application would then own.
    /// </para>
    /// <para>
    /// The level is left to configuration rather than set here. The host already binds the logging
    /// section with reload, so writing the level into the settings file is what makes the verbose
    /// switch take effect without a restart — and calling <c>SetMinimumLevel</c> would add a rule
    /// that competes with the one the file provides. The in-memory default below is what the file
    /// overrides, and it is why an absent setting still means Information rather than everything.
    /// </para>
    /// </remarks>
    private static void ConfigureLogging(ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.ClearProviders();
        logging.AddConfiguration(configuration.GetSection(LoggingSectionName));

        logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = EventTimestampFormat;
        });

        logging.AddFile(LogFilePath, options =>
        {
            options.Append = true;
            options.FileSizeLimitBytes = LogFileSizeLimitBytes;
            options.MaxRollingFiles = LogFilesKept;
        });
    }

    private static string LogFilePath => Path.Combine(ApplicationPaths.LogFolder, LogFileName);

    /// <summary>
    /// The level that applies before the user has ever opened the settings window, expressed as
    /// configuration so that the settings file can override it rather than fight it.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string?>> DefaultLogLevel =>
        [new(LogLevelDefaultKey, nameof(LogLevel.Information))];
}
