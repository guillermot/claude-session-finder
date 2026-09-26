using SessionFinder.Core.Results;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Whether the launcher is started by the operating system when the user signs in.
/// </summary>
/// <remarks>
/// <para>
/// There is deliberately no toggle here. The settings file is the single source of truth and this
/// registration is a derived copy of one value in it, which is why the only operations are reading
/// what the system currently believes and making it agree. A user who edits the settings file by
/// hand, or copies it to another machine, gets the registration they asked for without having to
/// know the registration exists.
/// </para>
/// <para>
/// The methods are synchronous because every implementation of this port writes a few bytes to a
/// local per-user store. An asynchronous signature would buy no concurrency and would suggest the
/// call is worth awaiting.
/// </para>
/// </remarks>
public interface IAutostart
{
    /// <summary>
    /// Reads whether the operating system is currently set to start this application at sign-in.
    /// </summary>
    /// <returns>
    /// The current registration, or a failure when the per-user store could not be read. A machine
    /// whose policy denies this is a fact to report, not a reason to fail the start-up.
    /// </returns>
    Result<bool> ReadIsEnabled();

    /// <summary>
    /// Makes the operating system registration agree with the setting.
    /// </summary>
    /// <param name="shouldStartWithSession">What the settings file says.</param>
    /// <returns>Success, or the reason the registration could not be changed.</returns>
    Result Apply(bool shouldStartWithSession);
}
