using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Reopens a session in a terminal, in the folder it was started from.
/// </summary>
/// <remarks>
/// The folder precondition is stricter here than it looks. For opening an editor an unknown folder
/// merely leaves nothing to open; for a resume it makes the operation impossible, because the
/// identifier is only resolvable from the originating directory and there is no directory-free form
/// of the command to fall back to.
/// </remarks>
/// <param name="locator">Finds the terminal.</param>
/// <param name="commands">Shapes the command that terminal is given.</param>
/// <param name="launcher">Starts it.</param>
public sealed class ResumeSessionHandler(
    ITerminalLocator locator,
    ITerminalCommandFactory commands,
    IShellLauncher launcher) : IResumeSessionHandler
{
    /// <inheritdoc />
    public async Task<Result> HandleAsync(
        ResumeSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var precondition = FolderPrecondition.Check(command.Folder);

        if (precondition.IsFailure)
        {
            return precondition;
        }

        var terminal = locator.Locate();

        if (terminal.IsFailure)
        {
            return Result.Failure(terminal.Error!);
        }

        var shellCommand = commands.CreateResumeCommand(
            terminal.Value,
            command.Folder,
            command.SessionId);

        return await launcher.LaunchAsync(shellCommand, cancellationToken).ConfigureAwait(false);
    }
}
