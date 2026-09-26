namespace SessionFinder.Mac.Platform;

/// <summary>
/// Works out whether this process is running from inside a <c>.app</c> bundle, and which one.
/// </summary>
/// <remarks>
/// The executable inside a bundle sits at <c>Something.app/Contents/MacOS/Something</c>, so the
/// bundle is three directories up and is recognised by its extension. Running from a build output
/// instead — which is how the application is started while it is being worked on — has no bundle,
/// and the callers of this treat that as a fact to report rather than a failure: autostart is the
/// only feature that needs one, and a bundle-less build is not a build anybody wants registered to
/// start at sign-in.
/// </remarks>
internal static class ApplicationBundle
{
    private const string BundleExtension = ".app";
    private const int DirectoriesAboveTheExecutable = 3;

    /// <summary>
    /// Finds the bundle this process is running from.
    /// </summary>
    /// <returns>The absolute path of the <c>.app</c> bundle, or <see langword="null"/>.</returns>
    public static string? Path()
    {
        // Bundle/Contents/MacOS/executable: three steps up from the executable itself.
        var directory = Environment.ProcessPath;

        for (var step = 0; step < DirectoriesAboveTheExecutable && directory is not null; step++)
        {
            directory = System.IO.Path.GetDirectoryName(directory);
        }

        return directory is not null
            && directory.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(directory)
                ? directory
                : null;
    }
}
