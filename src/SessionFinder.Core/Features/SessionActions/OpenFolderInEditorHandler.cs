using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Opens a session's folder in the editor found on this machine.
/// </summary>
/// <remarks>
/// The editor is located on every invocation rather than once at startup. An editor that was
/// installed, moved or uninstalled while the launcher sat in the notification area is a normal
/// thing to happen over the days this process stays alive, and the search costs a handful of
/// existence checks against a key press.
/// </remarks>
/// <param name="locator">Finds the editor.</param>
/// <param name="launcher">Starts it.</param>
public sealed class OpenFolderInEditorHandler(IEditorLocator locator, IShellLauncher launcher)
    : IOpenFolderInEditorHandler
{
    /// <inheritdoc />
    public async Task<Result> HandleAsync(
        OpenFolderInEditorCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var precondition = FolderPrecondition.Check(command.Folder);

        if (precondition.IsFailure)
        {
            return precondition;
        }

        var editor = locator.Locate();

        if (editor.IsFailure)
        {
            return Result.Failure(editor.Error!);
        }

        return await launcher
            .LaunchAsync(BuildCommand(editor.Value, command), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The editor is the one program here whose exit code was measured to be worth reading: it
    /// returns zero whether it opened a window of its own or handed the folder to an instance that
    /// was already running, and nonzero when it could not open the folder at all.
    /// </summary>
    private static ShellCommand BuildCommand(LaunchProgram editor, OpenFolderInEditorCommand command) => new()
    {
        Executable = editor.Executable,
        Arguments = [.. editor.LeadingArguments, command.Folder.Display],
        WorkingDirectory = command.Folder.Display,
        ConsoleWindow = editor.ConsoleWindow,
        IsExitCodeMeaningful = true,
    };
}
