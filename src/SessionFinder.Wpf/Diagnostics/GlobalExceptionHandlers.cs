using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Wpf.Diagnostics;

/// <summary>
/// The last seam: whatever escaped every other one is logged, shown, and survived.
/// </summary>
/// <remarks>
/// <para>
/// Three sources, because the runtime has three and they do not overlap. An exception on the user
/// interface thread arrives at the dispatcher; one on a thread-pool thread with nobody awaiting it
/// arrives at the task scheduler once the task is collected; anything else ends the process unless
/// it is seen first. A handler for one of them is not a handler for the others.
/// </para>
/// <para>
/// The dispatcher's exception is marked handled and the application keeps running. That is a
/// deliberate choice and not a general one: this is a tray application whose window is a search box,
/// so the worst outcome of carrying on is a view that needs reopening, whereas the outcome of
/// ending is a tray icon that vanishes while the user is looking at something else. A search box
/// that dies silently is worse than one showing an error.
/// </para>
/// <para>
/// The unhandled-exception event of the application domain cannot be cancelled: by the time it is
/// raised the process is going down. It is subscribed to anyway, because the account of why is the
/// only thing left that has any value.
/// </para>
/// </remarks>
internal sealed class GlobalExceptionHandlers
{
    private const string FailureTitle = "Something went wrong";
    private const string FailureMessage =
        "The application is still running. The log has the detail.";

    private readonly IUserNotifier _notifier;
    private readonly ILogger<GlobalExceptionHandlers> _logger;

    private GlobalExceptionHandlers(IUserNotifier notifier, ILogger<GlobalExceptionHandlers> logger)
    {
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// Subscribes to the three sources of unobserved failure.
    /// </summary>
    /// <param name="application">The application whose dispatcher is watched.</param>
    /// <param name="notifier">How the failure reaches the user.</param>
    /// <param name="logger">Where the failure is recorded.</param>
    /// <returns>The installed handlers, which live as long as the process.</returns>
    public static GlobalExceptionHandlers Install(
        Application application,
        IUserNotifier notifier,
        ILogger<GlobalExceptionHandlers> logger)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(logger);

        var handlers = new GlobalExceptionHandlers(notifier, logger);

        application.DispatcherUnhandledException += handlers.OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += handlers.OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += handlers.OnUnobservedTaskException;

        return handlers;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        Report(e.Exception, UnhandledExceptionLog.OnUserInterfaceThread);
    }

    /// <summary>
    /// Runs while the process is already ending, so it logs and does not try to show anything: a
    /// notification raised here would be drawn after the icon it belongs to has gone.
    /// </summary>
    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            UnhandledExceptionLog.ProcessEnding(_logger, exception);
        }
    }

    /// <summary>
    /// A faulted task nobody awaited. It is marked observed because the alternative on an
    /// unconfigured runtime is a process that ends for something that had no user waiting on it.
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();

        UnhandledExceptionLog.UnobservedTaskFailed(_logger, e.Exception);
    }

    private void Report(Exception exception, string origin)
    {
        UnhandledExceptionLog.Survived(_logger, exception, origin);

        _notifier.Notify(UserNotification.Error(FailureTitle, FailureMessage));
    }
}
