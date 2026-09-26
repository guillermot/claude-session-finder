using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SessionFinder.Mac.Diagnostics;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Mac.Composition;

/// <summary>
/// Starts and stops the generic host around the run loop, without ever blocking the thread the loop
/// runs on.
/// </summary>
/// <remarks>
/// <para>
/// The host is started off the user interface thread so that the first pass over the transcripts,
/// which reads every file under the Claude configuration directory, cannot delay the menu-bar item
/// or the first press of the chord. It is stopped from inside the loop rather than after it, so
/// that the shutdown stays awaited, bounded and observable — with the side effect that the window
/// stays responsive while the indexer finishes the transcript in its hands.
/// </para>
/// <para>
/// A failure to start is survived rather than fatal. The index already on disk is still searchable,
/// so the honest outcome is an application that says what it lost and keeps working, not one that
/// disappears.
/// </para>
/// </remarks>
/// <param name="host">The host holding every service, including the indexer.</param>
/// <param name="lifetime">The lifetime whose run loop is ended once the host has stopped.</param>
/// <param name="dispatcher">The way back to the user interface thread from a background failure.</param>
/// <param name="notifier">How a failure is reported when no window is on screen.</param>
/// <param name="logger">Where the detail of a failure is recorded.</param>
/// <param name="shutdownBudget">How long the host is given to stop before it is abandoned.</param>
internal sealed class HostLifecycle(
    IHost host,
    IClassicDesktopStyleApplicationLifetime lifetime,
    IUiDispatcher dispatcher,
    IUserNotifier notifier,
    ILogger<HostLifecycle> logger,
    TimeSpan shutdownBudget)
{
    private const string StartFailureTitle = "The index is not being updated";
    private const string StartFailureMessage =
        "Sessions already indexed can still be searched, but new messages will not appear. "
        + "The log has the detail.";

    private Task _startup = Task.CompletedTask;
    private int _ending;

    /// <summary>
    /// Begins starting the host and returns at once. Call before entering the run loop.
    /// </summary>
    /// <remarks>
    /// The host's own stop signal is routed into the same exit as the menu. The host listens for
    /// SIGTERM and SIGINT and answers them by cancelling its stopping token and nothing else — it
    /// expects its caller to be blocked in <c>Run</c>, which this one is not — so without this, a
    /// logout or a <c>launchctl</c> stop would leave the loop running and the process alive.
    /// </remarks>
    public void Begin()
    {
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.Register(
            () => dispatcher.Post(() => OnExitRequested(this, EventArgs.Empty)));

        _startup = StartAsync();
    }

    /// <summary>
    /// Stops the host and then ends the run loop. Written as an event handler because that is what
    /// it is: the menu asking the application to end.
    /// </summary>
    /// <param name="sender">Ignored.</param>
    /// <param name="e">Ignored.</param>
    public async void OnExitRequested(object? sender, EventArgs e) => await EndAsync();

    private async Task StartAsync()
    {
        try
        {
            await Task.Run(() => host.StartAsync(CancellationToken.None));
        }
        catch (Exception exception)
        {
            StartupLog.HostFailedToStart(logger, exception);

            dispatcher.Post(() => notifier.Notify(
                UserNotification.Error(StartFailureTitle, StartFailureMessage)));
        }
    }

    /// <summary>
    /// Waits for a start still in flight before stopping, so that a user who exits within the first
    /// seconds does not leave a half-started host behind, and ends the loop whatever happens.
    /// </summary>
    private async Task EndAsync()
    {
        // Stopping the host raises its stopping token, which is routed back here; only the first
        // request does the work.
        if (Interlocked.Exchange(ref _ending, 1) != 0)
        {
            return;
        }

        try
        {
            await _startup;
            await host.StopAsync(shutdownBudget);
        }
        catch (Exception exception)
        {
            StartupLog.HostFailedToStop(logger, exception);
        }
        finally
        {
            lifetime.Shutdown();
        }
    }
}
