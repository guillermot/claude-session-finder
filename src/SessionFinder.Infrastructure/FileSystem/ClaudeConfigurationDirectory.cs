namespace SessionFinder.Infrastructure.FileSystem;

/// <summary>
/// Works out where Claude Code keeps its configuration, and therefore its transcripts.
/// </summary>
/// <remarks>
/// The precedence is the one the tool itself uses: an explicitly configured directory wins, then
/// the <c>CLAUDE_CONFIG_DIR</c> environment variable, then the conventional folder in the user's
/// home directory. The resolution is a pure function of its three inputs so it can be verified
/// without touching the environment of the test process.
/// </remarks>
public static class ClaudeConfigurationDirectory
{
    /// <summary>Environment variable that relocates the whole configuration directory.</summary>
    public const string EnvironmentVariableName = "CLAUDE_CONFIG_DIR";

    /// <summary>Conventional configuration folder inside the user's home directory.</summary>
    public const string DefaultFolderName = ".claude";

    /// <summary>Subfolder holding one directory of transcripts per project.</summary>
    public const string ProjectsFolderName = "projects";

    /// <summary>
    /// Applies the precedence to explicit settings, the environment and the user's home directory.
    /// </summary>
    /// <param name="configuredDirectory">The directory from settings, usually blank.</param>
    /// <param name="environmentDirectory">The value of <see cref="EnvironmentVariableName"/>.</param>
    /// <param name="userProfileDirectory">The user's home directory.</param>
    /// <returns>The configuration directory to read from.</returns>
    public static string Resolve(
        string? configuredDirectory,
        string? environmentDirectory,
        string userProfileDirectory)
    {
        ArgumentNullException.ThrowIfNull(userProfileDirectory);

        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return Path.GetFullPath(configuredDirectory);
        }

        if (!string.IsNullOrWhiteSpace(environmentDirectory))
        {
            return Path.GetFullPath(environmentDirectory);
        }

        return Path.Combine(userProfileDirectory, DefaultFolderName);
    }

    /// <summary>
    /// Applies the precedence using the current process environment.
    /// </summary>
    /// <param name="configuredDirectory">The directory from settings, usually blank.</param>
    /// <returns>The configuration directory to read from.</returns>
    public static string Resolve(string? configuredDirectory) => Resolve(
        configuredDirectory,
        Environment.GetEnvironmentVariable(EnvironmentVariableName),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
}
