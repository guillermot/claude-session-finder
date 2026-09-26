using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using SessionFinder.Core;
using SessionFinder.Core.Configuration;
using SessionFinder.Infrastructure;
using SessionFinder.Infrastructure.FileSystem;
using SessionFinder.Presentation;

namespace SessionFinder.Mac.Composition;

/// <summary>
/// Builds the generic host that owns every service in the macOS head, including the background
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
/// takes the working directory of whatever started it, and an application launched from Finder or
/// by a launch agent has no meaningful working directory at all — it inherits <c>/</c>.
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

    /// <summary>The chord this head asks for before the user has ever chosen one.</summary>
    /// <remarks>
    /// Not the Windows default, because on this platform both obvious spellings of it are taken:
    /// Command with Space is Spotlight and Command with Option and Space opens Finder's search
    /// window. Option with Space is what the launchers people already use on a Mac claim, which is
    /// evidence that it is both free and comfortable.
    /// </remarks>
    public const string DefaultHotkey = "Alt+Space";

    /// <summary>
    /// Composes the host: Core handlers, Infrastructure adapters, the background indexer, the view
    /// models and the macOS adapters.
    /// </summary>
    /// <param name="shutdownTimeout">How long the host waits for the indexer to stop.</param>
    /// <returns>A host the caller must dispose.</returns>
    public static IHost Create(TimeSpan shutdownTimeout)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Configuration.AddInMemoryCollection(PlatformDefaults);
        builder.Configuration.AddJsonFile(ApplicationPaths.SettingsFile, optional: true, reloadOnChange: true);

        ConfigureLogging(builder.Logging, builder.Configuration);

        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = shutdownTimeout);

        builder.Services.AddSessionFinderCore(builder.Configuration);
        builder.Services.AddSessionFinderInfrastructure(builder.Configuration);
        builder.Services.AddSessionFinderIndexer();
        builder.Services.AddSessionFinderPresentation(builder.Configuration);
        builder.Services.AddMacShell();

        return builder.Build();
    }

    /// <summary>
    /// Sends the account of what happened to a rolling file, and to the console for anyone who
    /// started the executable from a terminal.
    /// </summary>
    /// <remarks>
    /// The level is left to configuration rather than set here. The host already binds the logging
    /// section with reload, so writing the level into the settings file is what makes the verbose
    /// switch take effect without a restart. The in-memory defaults below are what the settings
    /// file overrides, and they are why an absent setting still means Information rather than
    /// everything.
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
    /// What applies before the user has ever opened the settings window, expressed as configuration
    /// so that the settings file overrides it rather than fights it.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string?>> PlatformDefaults =>
    [
        new(LogLevelDefaultKey, nameof(LogLevel.Information)),
        new($"{ShellOptions.SectionName}:{nameof(ShellOptions.Hotkey)}", DefaultHotkey),
    ];
}
