using System.IO;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Looks for a program on the search path.
/// </summary>
/// <remarks>
/// The platform has no managed API for "where would the shell find this", and the locators need the
/// answer for two different programs, so the walk lives here once rather than twice. Directories
/// that cannot be read are skipped: a search path routinely names folders on drives that are not
/// mounted, and that is not a reason to fail to find an editor.
/// </remarks>
internal static class ExecutableSearch
{
    private const string PathVariable = "PATH";
    private const char PathSeparator = ';';

    /// <summary>
    /// Finds the first directory on the search path holding a file with the given name.
    /// </summary>
    /// <param name="fileName">The file to look for, with its extension.</param>
    /// <returns>The full path of the file, or <see langword="null"/> when it is not on the path.</returns>
    public static string? Find(string fileName)
    {
        var searchPath = Environment.GetEnvironmentVariable(PathVariable);

        if (string.IsNullOrWhiteSpace(searchPath))
        {
            return null;
        }

        foreach (var directory in searchPath.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = CombineOrNull(directory.Trim(), fileName);

            if (candidate is not null && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? CombineOrNull(string directory, string fileName)
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
