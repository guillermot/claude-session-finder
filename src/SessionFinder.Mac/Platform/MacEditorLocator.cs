using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Finds Visual Studio Code on a Mac.
/// </summary>
/// <remarks>
/// <para>
/// The order is deliberate. A configured path is taken on trust, because the reason to set one is
/// an installation this search would not find. Then the <c>code</c> shim, which accepts a folder
/// and hands it to a running instance; then the copy of that same shim inside the application
/// bundle, because the one on the <c>PATH</c> is a symlink the user has to have created on purpose
/// and many never do.
/// </para>
/// <para>
/// The last resort is <c>open -a</c>, which asks the operating system to launch the application by
/// name. It works without the shim existing at all, and it is last because it cannot tell an
/// installation that is missing from one that merely refused: it reports success either way.
/// </para>
/// </remarks>
/// <param name="options">The settings, for an explicitly configured editor path.</param>
internal sealed class MacEditorLocator(IOptionsMonitor<ShellOptions> options) : IEditorLocator
{
    private const string CodeShimName = "code";
    private const string OpenExecutable = "/usr/bin/open";
    private const string ApplicationFlag = "-a";
    private const string ApplicationName = "Visual Studio Code";

    private static readonly string[] BundleShimPaths =
    [
        "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code",
        "/opt/homebrew/bin/code",
        "/usr/local/bin/code",
    ];

    /// <inheritdoc />
    public Result<LaunchProgram> Locate()
    {
        var configured = options.CurrentValue.EditorPath;

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Executable(configured);
        }

        if (ExecutableSearch.Find(CodeShimName) is { } onPath)
        {
            return Executable(onPath);
        }

        foreach (var candidate in BundleShimPaths)
        {
            if (File.Exists(candidate))
            {
                return Executable(candidate);
            }
        }

        return UserProfileBundleShim() is { } inHomeFolder
            ? Executable(inHomeFolder)
            : OpenByApplicationName();
    }

    /// <summary>
    /// The same bundle path again, under the user's own applications folder. Kept apart from the
    /// constant list because it has to be built from the home directory at the time of the call.
    /// </summary>
    private static string? UserProfileBundleShim()
    {
        var candidate = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Applications",
            "Visual Studio Code.app",
            "Contents",
            "Resources",
            "app",
            "bin",
            "code");

        return File.Exists(candidate) ? candidate : null;
    }

    private static Result<LaunchProgram> Executable(string path) =>
        Result<LaunchProgram>.Success(new LaunchProgram { Executable = path });

    private static Result<LaunchProgram> OpenByApplicationName() =>
        Result<LaunchProgram>.Success(new LaunchProgram
        {
            Executable = OpenExecutable,
            LeadingArguments = [ApplicationFlag, ApplicationName],
        });
}
