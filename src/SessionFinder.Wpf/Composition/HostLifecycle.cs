using System.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Wpf.Diagnostics;

namespace SessionFinder.Wpf.Composition;

/// <summary>
/// Starts and stops the generic host around the message loop, without ever blocking the thread the
/// loop runs on.
/// </summary>
/// <remarks>
/// <para>
/// The host is started off the user interface thread so that the first pass over the transcripts,
/// which reads every file under the Claude configuration directory, cannot delay the tray icon or
/// the first press of the chord. It is stopped from inside the loop rather than after it, which is
/// the part worth explaining: the entry point cannot await, because an asynchronous entry point does
/// not get the single-threaded apartment that every user interface component in this process
/// requires, and waiting on the stop from the loop's own thread once the loop has ended would be
/// blocking on asynchronous work in the one place it is least defensible. Stopping first and ending
/// the loop afterwards keeps the shutdown awaited, bounded and observable, and has the side effect
/// that the window stays responsive while the indexer finishes the transcript in its hands.
/// </para>
/// <para>
/// A failure to start is survived rather than fatal. The index already on disk is still searchable,
/// so the honest outcome is an application that says what it lost and keeps working, not one that
/// disappears.
/// </para>
/// </remarks>
/// <param name="host">The host holding every service, including the indexer.</param>
/// <param name="application">The application whose message loop is ended once the host has stopped.</param>
/// <param name="dispatcher">The way back to the user interface thread from a background failure.</param>
/// <param name="notifier">How a failure is reported when no window is on screen.</param>
/// <param name="logger">Where the detail of a failure is recorded.</param>
/// <param name="shutdownBudget">How long the host is given to stop before it is abandoned.</param>
internal sealed class HostLifecycle(
    IHost host,
    Application application,
    IUiDispatcher dispatcher,
    IUserNotifier notifier,
    ILogger<HostLifecycle> logger,
    TimeSpan shutdownBudget)
{
    private const string StartFailureTitle = "The index is not being updated";
    private const string StartFailureMessage =
        "Sessions already indexed can still be searched, but new messages will not appear. The log has the detail.";

    private Task _startup = Task.CompletedTask;

    /// <summary>
    /// Begins starting the host and returns at once. Call before entering the message loop.
    /// </summary>
    public void Begin() => _startup = StartAsync();

    /// <summary>
    /// Stops the host and then ends the message loop. Written as an event handler because that is
    /// what it is: the tray menu asking the application to end.
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
            application.Shutdown();
        }
    }
}
