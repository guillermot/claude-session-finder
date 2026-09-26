using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Shows a session's folder in the system file manager.
/// </summary>
/// <remarks>
/// <para>
/// The file manager is part of the operating system rather than something that may or may not be
/// installed, so for as long as there was one head its name sat here as a constant and the seam was
/// argued against on the grounds that nothing would ever take a second implementation of it. A
/// second head took one. Two file managers now have to coexist in one solution, which a constant
/// cannot do, so the name moved behind <see cref="IFileManagerLocator"/> — a port whose
/// implementations are each a single name, and that is all it is for.
/// </para>
/// <para>
/// Nothing anywhere reads the exit code of what this starts, and the file manager is the reason:
/// it returns a nonzero code after opening a window perfectly successfully, so a launcher that
/// treated a nonzero code as failure would report every single reveal as broken.
/// </para>
/// </remarks>
/// <param name="locator">Names the file manager.</param>
/// <param name="launcher">Starts it.</param>
public sealed class RevealInFileExplorerHandler(
    IFileManagerLocator locator,
    IShellLauncher launcher) : IRevealInFileExplorerHandler
{
    /// <inheritdoc />
    public async Task<Result> HandleAsync(
        RevealInFileExplorerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var precondition = FolderPrecondition.Check(command.Folder);

        if (precondition.IsFailure)
        {
            return precondition;
        }

        var fileManager = locator.Locate();

        if (fileManager.IsFailure)
        {
            return Result.Failure(fileManager.Error!);
        }

        return await launcher
            .LaunchAsync(BuildCommand(fileManager.Value, command), cancellationToken)
            .ConfigureAwait(false);
    }

    private static ShellCommand BuildCommand(
        LaunchProgram fileManager,
        RevealInFileExplorerCommand command) => new()
        {
            Executable = fileManager.Executable,
            Arguments = [.. fileManager.LeadingArguments, command.Folder.Display],
            ConsoleWindow = fileManager.ConsoleWindow,
        };
}
