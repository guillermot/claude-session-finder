using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Builds the command that reopens a Claude Code session in a terminal on Windows.
/// </summary>
/// <remarks>
/// <para>
/// Measured on this machine: <c>claude --resume &lt;id&gt;</c> only finds a session when it is run
/// from the exact directory the session was started in. A subdirectory does not work, the parent
/// does not work, and an unrelated directory reports the identifier as unknown in the same words it
/// uses for an identifier that was never real. The working directory is therefore not a convenience
/// here — it is what makes the command work at all, and it is why every command this type produces
/// carries one.
/// </para>
/// <para>
/// The session is started through a command shell rather than directly because <c>claude</c> is a
/// batch shim rather than an executable, and the shell is told to stay open afterwards so that an
/// error is still on screen when the user looks at it instead of having vanished with the process.
/// </para>
/// </remarks>
public sealed class WindowsTerminalCommandFactory : ITerminalCommandFactory
{
    private const string ClaudeExecutable = "claude";
    private const string ResumeFlag = "--resume";
    private const string CommandShellExecutable = "cmd";
    private const string KeepShellOpenFlag = "/k";
    private const string TerminalHostDirectoryFlag = "-d";
    private const string ChangeDirectoryCommand = "cd";
    private const string ChangeDriveFlag = "/d";
    private const string CommandSeparator = "&&";

    /// <inheritdoc />
    public ShellCommand CreateResumeCommand(
        TerminalProgram terminal,
        WorkingFolder folder,
        SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        ArgumentNullException.ThrowIfNull(folder);

        return terminal.Kind switch
        {
            TerminalKind.TerminalHost => CreateTerminalHostCommand(terminal, folder, sessionId),
            _ => CreateCommandShellCommand(terminal, folder, sessionId),
        };
    }

    /// <inheritdoc />
    public string BuildResumeCommandLine(WorkingFolder folder, SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(folder);

        var changeDirectory = new ShellCommand
        {
            Executable = ChangeDirectoryCommand,
            Arguments = [ChangeDriveFlag, folder.Display],
        };

        var resume = new ShellCommand
        {
            Executable = ClaudeExecutable,
            Arguments = [ResumeFlag, sessionId.ToString()],
        };

        return $"{changeDirectory.ToCommandLine()} {CommandSeparator} {resume.ToCommandLine()}";
    }

    /// <summary>
    /// A terminal host is given the directory as an argument and then the whole shell invocation,
    /// because the directory it is told about is the one it hands to the shell it starts.
    /// </summary>
    private static ShellCommand CreateTerminalHostCommand(
        TerminalProgram terminal,
        WorkingFolder folder,
        SessionId sessionId) => new()
        {
            Executable = terminal.Executable,
            Arguments =
            [
                TerminalHostDirectoryFlag,
                folder.Display,
                CommandShellExecutable,
                KeepShellOpenFlag,
                ClaudeExecutable,
                ResumeFlag,
                sessionId.ToString(),
            ],
            WorkingDirectory = folder.Display,
            ConsoleWindow = ConsoleWindowMode.None,
        };

    /// <summary>
    /// A bare shell has nowhere to put a directory argument, so it is started in the directory
    /// instead, and it needs a console window of its own because the launching application has none
    /// to lend it.
    /// </summary>
    private static ShellCommand CreateCommandShellCommand(
        TerminalProgram terminal,
        WorkingFolder folder,
        SessionId sessionId) => new()
        {
            Executable = terminal.Executable,
            Arguments = [KeepShellOpenFlag, ClaudeExecutable, ResumeFlag, sessionId.ToString()],
            WorkingDirectory = folder.Display,
            ConsoleWindow = ConsoleWindowMode.Visible,
        };
}
