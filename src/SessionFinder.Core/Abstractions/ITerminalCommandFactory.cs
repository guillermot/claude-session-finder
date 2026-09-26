using SessionFinder.Core.Domain;
using SessionFinder.Core.Features.SessionActions;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Builds the command that reopens a Claude Code session in a terminal.
/// </summary>
/// <remarks>
/// <para>
/// Shaping the command is a port because the two operating systems disagree about more than a
/// program name. Windows reaches a resumed session by handing a command line to a shell that stays
/// open afterwards; macOS has no argv for that at all and has to ask the terminal application to
/// run the line for it. Quoting differs, the change of directory differs, and the second method
/// here — the pasteable line — never touches a launcher, so there was nowhere else for the
/// difference to be absorbed.
/// </para>
/// <para>
/// Implementations live in Core beside the slice rather than in a head. Deciding <em>which</em>
/// arguments a resume needs is the part worth testing and it must not be reachable only by
/// clicking; finding the terminal on a particular machine is the head's job, and that stays behind
/// <see cref="ITerminalLocator"/>.
/// </para>
/// </remarks>
public interface ITerminalCommandFactory
{
    /// <summary>
    /// Builds the command that opens a terminal in the session's folder and resumes the session
    /// there.
    /// </summary>
    /// <param name="terminal">The terminal that was found on this machine.</param>
    /// <param name="folder">The folder the session was started in.</param>
    /// <param name="sessionId">The session to resume.</param>
    /// <returns>The command to run.</returns>
    ShellCommand CreateResumeCommand(
        TerminalProgram terminal,
        WorkingFolder folder,
        SessionId sessionId);

    /// <summary>
    /// Renders the resume as a line a person can paste into a shell, including the change of
    /// directory without which the identifier will not be found.
    /// </summary>
    /// <param name="folder">The folder the session was started in.</param>
    /// <param name="sessionId">The session to resume.</param>
    /// <returns>The pasteable command line.</returns>
    string BuildResumeCommandLine(WorkingFolder folder, SessionId sessionId);
}
