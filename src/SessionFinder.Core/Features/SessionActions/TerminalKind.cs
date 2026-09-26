namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// Which terminal was found, because they take a working directory in completely different ways.
/// </summary>
/// <remarks>
/// The members are grouped by operating system and a locator only ever returns its own platform's
/// group: the Windows head cannot find iTerm and the macOS head cannot find the command shell. The
/// grouping is what lets each <see cref="SessionFinder.Core.Abstractions.ITerminalCommandFactory"/>
/// switch exhaustively over a small set instead of matching on an application name.
/// </remarks>
public enum TerminalKind
{
    /// <summary>
    /// A tabbed terminal host that is told the directory as an argument and then hands the rest of
    /// the line to the shell it starts.
    /// </summary>
    TerminalHost = 0,

    /// <summary>
    /// A bare command shell, which has no directory argument and is instead started in the
    /// directory by the process that launches it.
    /// </summary>
    CommandShell = 1,

    /// <summary>
    /// The macOS terminal that ships with the system, driven through AppleScript because it takes
    /// no command to run on its own command line.
    /// </summary>
    MacTerminalApp = 2,

    /// <summary>
    /// iTerm, driven through AppleScript as well but with a different verb: it is asked to create a
    /// window around the command rather than to type the command into one.
    /// </summary>
    MacITerm = 3,
}
