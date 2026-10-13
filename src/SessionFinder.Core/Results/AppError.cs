namespace SessionFinder.Core.Results;

/// <summary>
/// Something that went wrong in a way the user interface is expected to render rather than a way
/// the process is expected to fail on.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Code"/> is for logs and tests, which must not be coupled to prose that will be
/// reworded. The <see cref="Message"/> is for the user, so it says what happened and what can be
/// done about it rather than naming a type or an API.
/// </para>
/// <para>
/// Everything reachable from here is an expected outcome on a live machine: a session whose folder
/// was never recorded, an editor that is not installed, a clipboard another process is holding.
/// A corrupt index or a disk that is full is not one of these and is left as an exception.
/// </para>
/// </remarks>
public sealed record AppError
{
    /// <summary>Stable identifier for the failure, safe to assert on and to log.</summary>
    public required string Code { get; init; }

    /// <summary>What happened, phrased for the person who pressed the key.</summary>
    public required string Message { get; init; }

    /// <summary>
    /// The session never recorded a working folder, so there is nothing to open, reveal or resume
    /// in. This is the precondition the result list renders as a disabled action with a reason.
    /// </summary>
    public static AppError FolderUnknown { get; } = new()
    {
        Code = nameof(FolderUnknown),
        Message = "This session did not record the folder it was started from, "
            + "so it cannot be opened, revealed or resumed.",
    };

    /// <summary>No Visual Studio Code installation could be found in any of the known places.</summary>
    public static AppError EditorNotFound { get; } = new()
    {
        Code = nameof(EditorNotFound),
        Message = "Visual Studio Code was not found. Install it, or set the editor path in settings.",
    };

    /// <summary>No terminal could be found to host the resumed session.</summary>
    /// <remarks>
    /// The message names no particular terminal. Each head searches for the ones its own machine
    /// might have, and a user told to install Windows Terminal on a Mac would rightly stop reading.
    /// </remarks>
    public static AppError TerminalNotFound { get; } = new()
    {
        Code = nameof(TerminalNotFound),
        Message = "No terminal was found to resume the session in. "
            + "Set the terminal path in settings.",
    };

    /// <summary>The system file manager could not be named on this machine.</summary>
    /// <remarks>
    /// Close to unreachable, and kept for the same reason the locator behind it exists: the port
    /// returns a result, and a result has to be able to say no.
    /// </remarks>
    public static AppError FileManagerNotFound { get; } = new()
    {
        Code = nameof(FileManagerNotFound),
        Message = "No file manager was found to show the folder in.",
    };

    /// <summary>There is no activity in the recap, so there is nothing for a summary to say.</summary>
    public static AppError NothingToSummarize { get; } = new()
    {
        Code = nameof(NothingToSummarize),
        Message = "There is no activity on this day to summarise.",
    };

    /// <summary>The <c>claude</c> command could not be found to write the summary with.</summary>
    public static AppError SummarizerNotFound { get; } = new()
    {
        Code = nameof(SummarizerNotFound),
        Message = "The claude command was not found. Install Claude Code, or set its path under Recap in the settings file.",
    };

    /// <summary>
    /// Builds the failure for a summary the model was asked for and did not write.
    /// </summary>
    /// <param name="reason">Why, in the words of whatever refused.</param>
    /// <returns>The error.</returns>
    public static AppError SummaryFailed(string reason) => new()
    {
        Code = nameof(SummaryFailed),
        Message = $"The summary could not be written: {reason}",
    };

    /// <summary>
    /// Builds the failure for a command that could not be started at all.
    /// </summary>
    /// <param name="executable">The program that was going to be run.</param>
    /// <param name="reason">Why the attempt failed, in the words of whatever refused it.</param>
    /// <returns>The error.</returns>
    public static AppError LaunchFailed(string executable, string reason) => new()
    {
        Code = nameof(LaunchFailed),
        Message = $"{executable} could not be started: {reason}",
    };

    /// <summary>
    /// Builds the failure for a setting whose value would not work.
    /// </summary>
    /// <param name="setting">The setting that was rejected.</param>
    /// <param name="reason">Why it was rejected, phrased as the end of the sentence.</param>
    /// <returns>The error.</returns>
    public static AppError SettingRejected(string setting, string reason) => new()
    {
        Code = nameof(SettingRejected),
        Message = $"{setting} was not saved because {reason}.",
    };

    /// <summary>
    /// Builds the failure for settings that could not be written to disk.
    /// </summary>
    /// <param name="reason">Why the write failed, in the words of whatever refused it.</param>
    /// <returns>The error.</returns>
    public static AppError SettingsNotSaved(string reason) => new()
    {
        Code = nameof(SettingsNotSaved),
        Message = $"The settings file could not be written: {reason}",
    };

    /// <summary>
    /// Builds the failure for a start-with-sign-in registration that could not be read or changed.
    /// </summary>
    /// <param name="reason">Why the per-user store refused, in its own words.</param>
    /// <returns>The error.</returns>
    public static AppError AutostartUnavailable(string reason) => new()
    {
        Code = nameof(AutostartUnavailable),
        Message = $"Starting at login could not be configured: {reason}",
    };

    /// <summary>
    /// Builds the failure for a clipboard that stayed locked by another process for the whole of
    /// the retry window.
    /// </summary>
    /// <param name="reason">Why the last attempt failed.</param>
    /// <returns>The error.</returns>
    public static AppError ClipboardUnavailable(string reason) => new()
    {
        Code = nameof(ClipboardUnavailable),
        Message = $"The clipboard is being held by another application: {reason}",
    };
}
