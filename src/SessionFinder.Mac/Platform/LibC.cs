using System.Runtime.InteropServices;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// The handful of C library calls that have no managed equivalent.
/// </summary>
internal static partial class LibC
{
    /// <summary>
    /// Reads the effective user identifier, which is the number <c>launchd</c> names a per-user
    /// domain with. .NET exposes no way to ask for it.
    /// </summary>
    /// <returns>The effective user identifier.</returns>
    public static uint GetUserId() => geteuid();

    [LibraryImport("libc", EntryPoint = "geteuid")]
    private static partial uint geteuid();
}
