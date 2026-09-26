using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Names the macOS file manager.
/// </summary>
/// <remarks>
/// <para>
/// There is nothing to search for. <c>open</c> is part of the operating system and handing it a
/// folder shows that folder in Finder, which is what the Windows head gets from handing a folder to
/// Explorer.
/// </para>
/// <para>
/// Plain <c>open</c> rather than <c>open -R</c>. The flag reveals a path by selecting it inside its
/// parent, which for a folder means opening the folder above the one the user asked for — right for
/// a file, wrong for every target this application has.
/// </para>
/// </remarks>
internal sealed class FinderFileManagerLocator : IFileManagerLocator
{
    private const string OpenExecutable = "/usr/bin/open";

    /// <inheritdoc />
    public Result<LaunchProgram> Locate() => Result<LaunchProgram>.Success(new LaunchProgram
    {
        Executable = OpenExecutable,
        ConsoleWindow = ConsoleWindowMode.None,
    });
}
