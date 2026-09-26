using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Abstractions;
using SessionFinder.Core.Configuration;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Presentation.Shell;

/// <summary>
/// Keeps the operating system's start-at-sign-in registration in step with the setting.
/// </summary>
/// <remarks>
/// <para>
/// The direction is one-way on purpose: the settings file decides, and the registration is
/// rewritten whenever it disagrees. That makes the file the single source of truth in a way that
/// survives the registration being changed by something else — another tool, a cleanup utility, or
/// the same settings file carried to a second machine — rather than only while nothing else
/// touches it.
/// </para>
/// <para>
/// It is checked at start-up and again whenever the setting changes, and the check is a comparison
/// before a write. That is what makes the repeated notifications a configuration reload produces
/// harmless: two callbacks for one save cost two reads and no writes.
/// </para>
/// </remarks>
public sealed class AutostartReconciler : IDisposable
{
    private const string FailureTitle = "Start at login could not be set";

    private readonly IAutostart _autostart;
    private readonly IOptionsMonitor<ShellOptions> _options;
    private readonly IUserNotifier _notifier;
    private readonly ILogger<AutostartReconciler> _logger;

    private IDisposable? _subscription;
    private bool _isStarted;
    private bool _isDisposed;

    /// <summary>
    /// Builds the reconciler.
    /// </summary>
    /// <param name="autostart">The operating system's registration.</param>
    /// <param name="options">The setting, watched for changes.</param>
    /// <param name="notifier">How a refused registration reaches the user.</param>
    /// <param name="logger">Where the outcome is recorded.</param>
    public AutostartReconciler(
        IAutostart autostart,
        IOptionsMonitor<ShellOptions> options,
        IUserNotifier notifier,
        ILogger<AutostartReconciler> logger)
    {
        ArgumentNullException.ThrowIfNull(autostart);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(logger);

        _autostart = autostart;
        _options = options;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>
    /// Brings the registration into line with the setting and starts following it. Safe to call once.
    /// </summary>
    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        _subscription = _options.OnChange(_ => Reconcile());

        Reconcile();
    }

    /// <summary>Stops following the setting. The registration is left as it is.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _subscription?.Dispose();
    }

    private void Reconcile()
    {
        if (_isDisposed)
        {
            return;
        }

        var wanted = _options.CurrentValue.StartAtLogin;
        var current = _autostart.ReadIsEnabled();

        if (current.Error is { } readFailure)
        {
            Report(readFailure.Message);
            return;
        }

        if (current.Value == wanted)
        {
            return;
        }

        Write(wanted);
    }

    private void Write(bool wanted)
    {
        var applied = _autostart.Apply(wanted);

        if (applied.Error is { } failure)
        {
            Report(failure.Message);
            return;
        }

        AutostartLog.RegistrationRewritten(_logger, wanted);
    }

    private void Report(string message)
    {
        AutostartLog.RegistrationUnavailable(_logger, message);
        _notifier.Notify(UserNotification.Warning(FailureTitle, message));
    }
}
