namespace SessionFinder.Infrastructure.Recap;

/// <summary>
/// Finds the <c>claude</c> command line.
/// </summary>
/// <remarks>
/// <para>
/// The path is searched first, then the places the installers put the command. The second step is
/// not a fallback for odd machines: a launcher started at login or from the macOS menu bar inherits
/// the session's minimal path, not the one the user's shell builds, and the native installer puts
/// the command in <c>~/.local/bin</c>, which only the shell's path contains.
/// </para>
/// <para>
/// The file system is passed in as a function so the search order can be tested without creating
/// executables.
/// </para>
/// </remarks>
public static class ClaudeExecutableLocator
{
    private static readonly string[] PosixNames = ["claude"];
    private static readonly string[] WindowsNames = ["claude.exe", "claude.cmd"];

    /// <summary>
    /// Finds the command on this machine.
    /// </summary>
    /// <param name="configured">An explicit path from the settings, which wins when it exists.</param>
    /// <returns>The absolute path, or <see langword="null"/> when there is none.</returns>
    public static string? Find(string? configured) => Find(
        configured,
        Environment.GetEnvironmentVariable("PATH"),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        OperatingSystem.IsWindows(),
        File.Exists);

    /// <summary>
    /// Finds the command, with every fact about the machine passed in.
    /// </summary>
    /// <param name="configured">An explicit path from the settings.</param>
    /// <param name="pathVariable">The value of <c>PATH</c>.</param>
    /// <param name="home">The user's home folder.</param>
    /// <param name="applicationData">The user's roaming application data folder.</param>
    /// <param name="isWindows">Whether to look for Windows file names and folders.</param>
    /// <param name="exists">Whether a file exists.</param>
    /// <returns>The first match, or <see langword="null"/>.</returns>
    public static string? Find(
        string? configured,
        string? pathVariable,
        string home,
        string applicationData,
        bool isWindows,
        Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return exists(configured) ? configured : null;
        }

        var names = isWindows ? WindowsNames : PosixNames;
        var separator = isWindows ? ';' : ':';

        var folders = (pathVariable ?? string.Empty)
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(InstallFolders(home, applicationData, isWindows));

        return folders
            .SelectMany(folder => names.Select(name => Path.Combine(folder, name)))
            .FirstOrDefault(exists);
    }

    private static IEnumerable<string> InstallFolders(string home, string applicationData, bool isWindows)
    {
        yield return Path.Combine(home, ".local", "bin");
        yield return Path.Combine(home, ".claude", "local");

        if (isWindows)
        {
            yield return Path.Combine(applicationData, "npm");
            yield break;
        }

        yield return "/opt/homebrew/bin";
        yield return "/usr/local/bin";
    }
}
