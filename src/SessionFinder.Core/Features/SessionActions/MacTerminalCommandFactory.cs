using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Domain;

namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Builds the command that reopens a Claude Code session in a terminal on macOS.
/// </summary>
/// <remarks>
/// <para>
/// The directory is what makes the resume work at all, exactly as it is on Windows:
/// <c>claude --resume &lt;id&gt;</c> resolves the identifier only when it runs from the directory
/// the session was started in. What differs is how a terminal is told to run something. Neither
/// Terminal nor iTerm accepts a command to run on its own command line — handing either a path only
/// opens a window there — so the command is delivered through AppleScript, and the program this
/// factory actually starts is <c>osascript</c> rather than a terminal.
/// </para>
/// <para>
/// Both scripts type the line into a shell rather than running it as the window's process. That is
/// the counterpart of <c>cmd /k</c> on Windows and it is chosen for the same reason: when the
/// resume fails, the message has to still be on screen when the user looks, instead of having
/// closed the window on its way out.
/// </para>
/// </remarks>
public sealed class MacTerminalCommandFactory : ITerminalCommandFactory
{
    private const string ClaudeExecutable = "claude";
    private const string ResumeFlag = "--resume";
    private const string ChangeDirectoryCommand = "cd";
    private const string CommandSeparator = "&&";
    private const string OsaScriptExecutable = "/usr/bin/osascript";
    private const string ScriptLineFlag = "-e";
    private const string TerminalApplicationName = "Terminal";
    private const string ITermApplicationName = "iTerm";

    /// <inheritdoc />
    public ShellCommand CreateResumeCommand(
        TerminalProgram terminal,
        WorkingFolder folder,
        SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        ArgumentNullException.ThrowIfNull(folder);

        var line = BuildResumeCommandLine(folder, sessionId);

        return new ShellCommand
        {
            Executable = OsaScriptExecutable,
            Arguments = terminal.Kind switch
            {
                TerminalKind.MacITerm => ITermScript(line),
                _ => TerminalAppScript(line),
            },
            ConsoleWindow = ConsoleWindowMode.None,
            IsExitCodeMeaningful = true,
        };
    }

    /// <inheritdoc />
    public string BuildResumeCommandLine(WorkingFolder folder, SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(folder);

        var changeDirectory = $"{ChangeDirectoryCommand} {PosixQuoting.Quote(folder.Display)}";
        var resume = $"{ClaudeExecutable} {ResumeFlag} {sessionId}";

        return $"{changeDirectory} {CommandSeparator} {resume}";
    }

    /// <summary>
    /// Terminal is asked to run the line as though it had been typed, which is what leaves a shell
    /// behind when the line finishes. It is activated separately because <c>do script</c> opens the
    /// window without bringing the application forward.
    /// </summary>
    private static string[] TerminalAppScript(string line) =>
    [
        ScriptLineFlag,
        $"tell application \"{TerminalApplicationName}\" to do script \"{EscapeForAppleScript(line)}\"",
        ScriptLineFlag,
        $"tell application \"{TerminalApplicationName}\" to activate",
    ];

    /// <summary>
    /// iTerm needs the window before it has a session to write to, so this is the one script with
    /// more than one statement. Writing text to the new session is preferred over creating the
    /// window around the command, which would end the session the moment the command did.
    /// </summary>
    private static string[] ITermScript(string line) =>
    [
        ScriptLineFlag,
        $"tell application \"{ITermApplicationName}\"",
        ScriptLineFlag,
        "activate",
        ScriptLineFlag,
        "set newWindow to (create window with default profile)",
        ScriptLineFlag,
        $"tell current session of newWindow to write text \"{EscapeForAppleScript(line)}\"",
        ScriptLineFlag,
        "end tell",
    ];

    /// <summary>
    /// Escapes a line for the inside of an AppleScript string literal.
    /// </summary>
    /// <remarks>
    /// Backslashes are doubled before quotes are escaped, because the other order would go back
    /// over the backslashes it had just added and double them a second time. The POSIX quoting this
    /// runs over emits a backslash whenever a folder name contains an apostrophe, so this is a path
    /// that real folder names reach rather than a theoretical one.
    /// </remarks>
    private static string EscapeForAppleScript(string line) =>
        line.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
