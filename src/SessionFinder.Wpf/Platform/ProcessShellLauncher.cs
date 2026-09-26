using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Starts commands as operating-system processes.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here waits for what it starts. Success is what it honestly is — the process was created —
/// and the window the user is about to work in is not something a launcher may block on.
/// </para>
/// <para>
/// A process that was created is not the same as a program that ran, though, and the difference used
/// to be invisible: an editor that died a fifth of a second later left an action that reported
/// success, a window that dismissed itself, and not one line anywhere. So every launch is written
/// down, and a command that says its exit code means something is watched for a moment afterwards.
/// Only for a moment: a program still alive when the window closes is a program that opened.
/// </para>
/// <para>
/// The console mode decides whether a shell is used to start the process. That is not decoration:
/// an application with no console of its own cannot lend one to a child, so the shell that is meant
/// to stay open and show a resumed session has to be started through the operating system shell to
/// get a window at all. That is also the one mode whose environment cannot be touched, because a
/// process started through the shell inherits this one's environment wholesale and the API that
/// would change it is refused. Nothing is lost by it: what has to be scrubbed only matters to an
/// Electron executable, and the shell path exists for a batch file running an interpreter.
/// </para>
/// </remarks>
/// <param name="logger">Where every launch and every failure is recorded.</param>
/// <param name="notifier">How a program that died on the doorstep reaches the user.</param>
/// <param name="dispatcher">The thread the notification-area icon belongs to.</param>
internal sealed class ProcessShellLauncher(
    ILogger<ProcessShellLauncher> logger,
    IUserNotifier notifier,
    IUiDispatcher dispatcher) : IShellLauncher
{
    private const int SuccessExitCode = 0;
    private const string InheritedWorkingDirectory = "the launcher's own folder";
    private const string ImmediateExitTitle = "The program did not stay open";

    private static readonly TimeSpan ImmediateExitWindow = TimeSpan.FromSeconds(3);

    /// <inheritdoc />
    public async Task<Result> LaunchAsync(ShellCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        cancellationToken.ThrowIfCancellationRequested();

        return await Task
            .Run(() => Start(command), cancellationToken)
            .ConfigureAwait(false);
    }

    private Result Start(ShellCommand command)
    {
        var arguments = command.ToArgumentString();

        ProcessLaunchLog.Starting(
            logger,
            command.Executable,
            arguments,
            command.WorkingDirectory ?? InheritedWorkingDirectory);

        try
        {
            Watch(Process.Start(BuildStartInfo(command, arguments)), command);

            return Result.Success();
        }
        catch (Win32Exception exception)
        {
            return Refuse(command, exception);
        }
        catch (InvalidOperationException exception)
        {
            return Refuse(command, exception);
        }
    }

    private Result Refuse(ShellCommand command, Exception exception)
    {
        ProcessLaunchLog.StartRefused(logger, command.Executable, exception);

        return Result.Failure(AppError.LaunchFailed(command.Executable, exception.Message));
    }

    /// <summary>
    /// Keeps the started process alive long enough to hear how it exited, when its exit code means
    /// anything, and lets go of it immediately when it does not.
    /// </summary>
    private void Watch(Process? process, ShellCommand command)
    {
        if (process is null)
        {
            return;
        }

        if (!command.IsExitCodeMeaningful)
        {
            process.Dispose();

            return;
        }

        _ = ReportIfItExitsImmediatelyAsync(process, command.Executable);
    }

    /// <summary>
    /// Waits out the moment in which a program that could not start would die, and reports it if it
    /// does. Detached on purpose: the result of the launch has already been returned, and the user
    /// is not made to wait for the editor to prove itself.
    /// </summary>
    private async Task ReportIfItExitsImmediatelyAsync(Process process, string executable)
    {
        using (process)
        {
            using var window = new CancellationTokenSource(ImmediateExitWindow);

            try
            {
                await process.WaitForExitAsync(window.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (process.ExitCode == SuccessExitCode)
            {
                return;
            }

            Report(executable, process.ExitCode);
        }
    }

    private void Report(string executable, int exitCode)
    {
        ProcessLaunchLog.ExitedImmediately(logger, executable, exitCode);

        var message = $"{Path.GetFileName(executable)} stopped with code {exitCode} straight after "
            + "starting, so nothing opened. The log has the command it was given.";

        dispatcher.Post(() => notifier.Notify(UserNotification.Warning(ImmediateExitTitle, message)));
    }

    /// <summary>
    /// Builds the process description, scrubbing the inherited environment wherever it can be
    /// reached. Setting it at all requires starting the process without the operating system shell,
    /// which is every mode but the one that wants a console window of its own.
    /// </summary>
    private static ProcessStartInfo BuildStartInfo(ShellCommand command, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.Executable,
            Arguments = arguments,
            WorkingDirectory = command.WorkingDirectory ?? string.Empty,
            UseShellExecute = command.ConsoleWindow == ConsoleWindowMode.Visible,
            CreateNoWindow = command.ConsoleWindow == ConsoleWindowMode.Suppressed,
        };

        if (!startInfo.UseShellExecute)
        {
            InheritedEnvironment.Scrub(startInfo.Environment);
        }

        return startInfo;
    }
}
