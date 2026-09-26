using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;

namespace SessionFinder.Core.Abstractions;

/// <summary>
/// Starts a program on the user's machine.
/// </summary>
/// <remarks>
/// The port takes an already-built <see cref="ShellCommand"/> and does nothing but start it. Every
/// decision about which program and which arguments belongs to the slice, so that changing what an
/// action does never means editing platform code.
/// </remarks>
public interface IShellLauncher
{
    /// <summary>
    /// Starts a command and returns as soon as it has started.
    /// </summary>
    /// <remarks>
    /// The result describes whether the command could be started, not what it then did. Waiting for
    /// a launched program to finish would mean waiting for an editor window the user is about to
    /// work in, and reading its exit code would misreport the file manager, which returns a nonzero
    /// code on success.
    /// </remarks>
    /// <param name="command">What to run.</param>
    /// <param name="cancellationToken">Abandons the attempt.</param>
    /// <returns>Success once the command has been started, or why it could not be.</returns>
    Task<Result> LaunchAsync(ShellCommand command, CancellationToken cancellationToken);
}
