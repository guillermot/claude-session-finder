using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;

namespace SessionFinder.Infrastructure.FileSystem;

/// <summary>
/// Resolves the three per-user locations the application owns.
/// </summary>
/// <remarks>
/// <para>
/// The static members exist because two callers need these paths before there is a container to
/// resolve anything from: the host has to add the settings file to its configuration before it can
/// bind options, and the file log provider has to be given a folder before the first log line. The
/// instance is what everything after that uses, so there is still exactly one answer.
/// </para>
/// <para>
/// On Windows, settings roam and the index and logs do not. A settings file is small and describes
/// a person; an index is a rebuildable cache of one machine's disk and a log is an account of one
/// machine's run, and neither is worth synchronising between them. macOS draws no such line, so
/// there the settings and the index share a folder and only the logs sit apart, which is where that
/// system keeps logs.
/// </para>
/// <para>
/// The branch on the operating system is not decoration. <see cref="Environment.SpecialFolder"/>
/// answers on macOS — it does not throw and it does not return nothing — but it answers with the
/// XDG-style <c>~/.config</c> and <c>~/.local/share</c> that .NET uses on Linux, which is not where
/// a Mac user, a backup or Finder looks for any of this. Writing there would have worked and been
/// wrong, and that is the kind of mistake nothing reports.
/// </para>
/// <para>
/// Each resolution is a pure function of its inputs, with a convenience overload that reads the
/// real environment, so the layout of a platform can be verified from the other one.
/// </para>
/// </remarks>
/// <param name="options">The bound settings; a blank index path selects the default location.</param>
public sealed class ApplicationPaths(IOptions<FinderOptions> options) : IApplicationPaths
{
    /// <summary>Folder the application owns inside whichever per-user root applies.</summary>
    public const string ApplicationFolderName = "ClaudeSessionFinder";

    private const string SettingsFileName = "settings.json";
    private const string IndexFileName = "index.db";
    private const string WindowsLogFolderName = "logs";
    private const string MacLibraryFolderName = "Library";
    private const string MacApplicationSupportFolderName = "Application Support";
    private const string MacLogsFolderName = "Logs";

    /// <inheritdoc />
    public string SettingsFilePath => SettingsFile;

    /// <inheritdoc />
    public string IndexFilePath => ResolveIndexPath(options.Value.IndexPath);

    /// <inheritdoc />
    public string LogFolderPath => LogFolder;

    /// <summary>Where the user's settings file is looked for, whether or not it exists.</summary>
    public static string SettingsFile => ResolveSettingsFile(
        OperatingSystem.IsMacOS(),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    /// <summary>Where the rolling log files are written.</summary>
    public static string LogFolder => ResolveLogFolder(
        OperatingSystem.IsMacOS(),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    /// <summary>
    /// Resolves where the settings file lives.
    /// </summary>
    /// <param name="isMacOs">Whether the macOS layout applies.</param>
    /// <param name="userProfileDirectory">The user's home directory.</param>
    /// <param name="roamingApplicationData">The Windows roaming application-data directory.</param>
    /// <returns>The absolute path of the settings file.</returns>
    public static string ResolveSettingsFile(
        bool isMacOs,
        string userProfileDirectory,
        string roamingApplicationData) => isMacOs
        ? Path.Combine(MacApplicationSupport(userProfileDirectory), SettingsFileName)
        : Path.Combine(roamingApplicationData, ApplicationFolderName, SettingsFileName);

    /// <summary>
    /// Resolves where the rolling log files are written.
    /// </summary>
    /// <param name="isMacOs">Whether the macOS layout applies.</param>
    /// <param name="userProfileDirectory">The user's home directory.</param>
    /// <param name="localApplicationData">The Windows local application-data directory.</param>
    /// <returns>The absolute path of the log folder.</returns>
    public static string ResolveLogFolder(
        bool isMacOs,
        string userProfileDirectory,
        string localApplicationData) => isMacOs
        ? Path.Combine(MacLibrary(userProfileDirectory), MacLogsFolderName, ApplicationFolderName)
        : Path.Combine(localApplicationData, ApplicationFolderName, WindowsLogFolderName);

    /// <summary>
    /// Resolves where the index lives.
    /// </summary>
    /// <param name="configuredPath">The configured path, which may be blank.</param>
    /// <param name="isMacOs">Whether the macOS layout applies.</param>
    /// <param name="userProfileDirectory">The user's home directory.</param>
    /// <param name="localApplicationData">The Windows local application-data directory.</param>
    /// <returns>The absolute path of the index database.</returns>
    public static string ResolveIndexPath(
        string? configuredPath,
        bool isMacOs,
        string userProfileDirectory,
        string localApplicationData)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        return isMacOs
            ? Path.Combine(MacApplicationSupport(userProfileDirectory), IndexFileName)
            : Path.Combine(localApplicationData, ApplicationFolderName, IndexFileName);
    }

    /// <summary>
    /// Resolves where the index lives, using the current process environment.
    /// </summary>
    /// <param name="configuredPath">The configured path, which may be blank.</param>
    /// <returns>The absolute path of the index database.</returns>
    public static string ResolveIndexPath(string? configuredPath) => ResolveIndexPath(
        configuredPath,
        OperatingSystem.IsMacOS(),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create));

    private static string MacLibrary(string userProfileDirectory) =>
        Path.Combine(userProfileDirectory, MacLibraryFolderName);

    private static string MacApplicationSupport(string userProfileDirectory) => Path.Combine(
        MacLibrary(userProfileDirectory),
        MacApplicationSupportFolderName,
        ApplicationFolderName);
}
