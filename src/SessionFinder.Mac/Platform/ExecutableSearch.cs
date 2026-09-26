namespace SessionFinder.Mac.Platform;

/// <summary>
/// Finds an executable on the <c>PATH</c>, which .NET has no managed equivalent of.
/// </summary>
/// <remarks>
/// The launcher is usually started at sign-in by <c>launchd</c> rather than from a shell, and a
/// process started that way inherits a short, unhelpful <c>PATH</c> that has none of the directories
/// a package manager writes to. That is why the callers of this type treat a miss as ordinary and
/// carry on to look in the places an application is actually installed.
/// </remarks>
internal static class ExecutableSearch
{
    private const char PathSeparator = ':';

    /// <summary>
    /// Looks for an executable file by name in every directory on the <c>PATH</c>.
    /// </summary>
    /// <param name="fileName">The file to look for, without a directory.</param>
    /// <returns>The absolute path of the first match, or <see langword="null"/>.</returns>
    public static string? Find(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = SafeCombine(directory, fileName);

            if (candidate is not null && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Joins a <c>PATH</c> entry to a file name, tolerating an entry that is not a usable path.
    /// A malformed entry is somebody else's mistake and is no reason to fail a key press.
    /// </summary>
    private static string? SafeCombine(string directory, string fileName)
    {
        try
        {
            return Path.Combine(directory, fileName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
