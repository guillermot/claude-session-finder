using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Features.SessionActions;
using SessionFinder.Core.Results;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Starts commands as operating-system processes.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here waits for what it starts. Success is what it honestly is — the process was created —
/// and the window the user is about to work in is not something a launcher may block on. A process
/// that was created is not the same as a program that ran, though, so a command that says its exit
/// code means something is watched for a moment afterwards. Only for a moment: a program still
/// alive when that moment closes is a program that opened.
/// </para>
/// <para>
/// Two things differ from the Windows launcher and both are consequences of the same fact, that
/// nothing on this platform is started through a shell. <see cref="ConsoleWindowMode"/> is read and
/// then ignored: it describes a console window that a child process on Windows may create, and
/// there is no such thing here — the window a resumed session appears in is opened by the terminal
/// application, not by anything this starts. And the arguments are passed as a list rather than as
/// a joined string, because <see cref="ShellCommand.ToArgumentString"/> quotes for the Windows
/// command-line parser and <see cref="ProcessStartInfo.ArgumentList"/> needs no quoting at all.
/// </para>
/// </remarks>
/// <param name="logger">Where every launch and every failure is recorded.</param>
/// <param name="notifier">How a program that died on the doorstep reaches the user.</param>
/// <param name="dispatcher">The thread the menu-bar item belongs to.</param>
internal sealed class MacShellLauncher(
    ILogger<MacShellLauncher> logger,
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

        return await Task.Run(() => Start(command), cancellationToken).ConfigureAwait(false);
    }

    private Result Start(ShellCommand command)
    {
        ProcessLaunchLog.Starting(
            logger,
            command.Executable,
            string.Join(' ', command.Arguments),
            command.WorkingDirectory ?? InheritedWorkingDirectory);

        try
        {
            Watch(Process.Start(BuildStartInfo(command)), command);

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

    private static ProcessStartInfo BuildStartInfo(ShellCommand command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.Executable,
            WorkingDirectory = command.WorkingDirectory ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        InheritedEnvironment.Scrub(startInfo.Environment);

        return startInfo;
    }
}
