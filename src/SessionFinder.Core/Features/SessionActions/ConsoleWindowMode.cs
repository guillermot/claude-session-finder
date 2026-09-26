namespace SessionFinder.Core.Features.SessionActions;

/// <summary>
/// What a command needs to happen to the console window that running it would create.
/// </summary>
/// <remarks>
/// This is stated as intent rather than as process-start flags because the three cases have three
/// different reasons behind them, and an adapter that only saw a pair of booleans would have to
/// guess which reason applied.
/// </remarks>
public enum ConsoleWindowMode
{
    /// <summary>
    /// The program draws its own windows and never creates a console. Applies to the editor and to
    /// the file manager.
    /// </summary>
    None = 0,

    /// <summary>
    /// The program runs in a console that the user must not see. Applies to the last-resort editor
    /// launch, which goes through a shell only to reach a batch file and would otherwise flash a
    /// black rectangle across the screen.
    /// </summary>
    Suppressed = 1,

    /// <summary>
    /// The console window is the point of the command: it is where the resumed session appears, and
    /// where an error has to stay legible instead of disappearing with the process.
    /// </summary>
    Visible = 2,
}
