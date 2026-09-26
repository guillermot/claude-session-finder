using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Names the Windows file manager.
/// </summary>
/// <remarks>
/// There is nothing to search for. Explorer is part of the operating system, it is on the path of
/// every Windows installation this application can run on, and a machine without it has problems
/// this launcher is not going to report first. The type exists only so that the name is on the
/// Windows side of the seam instead of inside the slice both heads share.
/// </remarks>
internal sealed class ExplorerFileManagerLocator : IFileManagerLocator
{
    private const string FileManagerExecutable = "explorer.exe";

    /// <inheritdoc />
    public Result<LaunchProgram> Locate() => Result<LaunchProgram>.Success(new LaunchProgram
    {
        Executable = FileManagerExecutable,
        ConsoleWindow = ConsoleWindowMode.None,
    });
}
