using Microsoft.Extensions.DependencyInjection;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Search;
using SessionFinder.Presentation.Shell;
using SessionFinder.Wpf.Platform;
using SessionFinder.Wpf.Views;

namespace SessionFinder.Wpf.Composition;

/// <summary>
/// The running shell: the three ways in — the chord, the tray menu and a second launch — joined to
/// the one window, and the teardown that puts the icon and the chord back.
/// </summary>
/// <remarks>
/// <para>
/// The policy lives in <see cref="ShellCoordinator"/>, which is platform-agnostic and tested. What
/// is left here is the part that cannot be: the single-instance gate, which is a Windows object and
/// reports a second launch from a thread-pool thread rather than from the user interface one.
/// </para>
/// <para>
/// Teardown is explicit rather than left to the container. Both the notification-area icon and the
/// message-only window behind the chord belong to the thread that runs the message loop, and a
/// container disposed from anywhere else would be releasing them from the wrong one. The second
/// disposal the container then performs is a no-op.
/// </para>
/// </remarks>
internal sealed class WindowsShell : IDisposable
{
    private readonly ShellCoordinator _coordinator;
    private readonly AutostartReconciler _autostart;
    private readonly NotifyIconTray _tray;
    private readonly Win32GlobalHotkey _hotkey;
    private readonly IUiDispatcher _dispatcher;
    private readonly IServiceProvider _services;
    private readonly SingleInstanceGate _gate;

    private SettingsWindow? _settingsWindow;
    private RecapWindow? _recapWindow;
    private bool _isDisposed;

    private WindowsShell(
        ShellCoordinator coordinator,
        AutostartReconciler autostart,
        NotifyIconTray tray,
        Win32GlobalHotkey hotkey,
        IUiDispatcher dispatcher,
        IServiceProvider services,
        SingleInstanceGate gate)
    {
        _coordinator = coordinator;
        _autostart = autostart;
        _tray = tray;
        _hotkey = hotkey;
        _dispatcher = dispatcher;
        _services = services;
        _gate = gate;
    }

    /// <summary>Raised when the user asks, from the tray menu, for the application to end.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// Resolves the shell, puts the tray icon up, claims a chord and starts listening for further
    /// launches. Must be called on the thread that will run the message loop.
    /// </summary>
    /// <param name="services">The composed service provider.</param>
    /// <param name="gate">The gate that reports a second launch.</param>
    /// <returns>The running shell, which the caller must dispose on the same thread.</returns>
    public static WindowsShell Start(IServiceProvider services, SingleInstanceGate gate)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(gate);

        var shell = new WindowsShell(
            services.GetRequiredService<ShellCoordinator>(),
            services.GetRequiredService<AutostartReconciler>(),
            services.GetRequiredService<NotifyIconTray>(),
            services.GetRequiredService<Win32GlobalHotkey>(),
            services.GetRequiredService<IUiDispatcher>(),
            services,
            gate);

        shell.Start();

        return shell;
    }

    /// <summary>Releases the chord, takes the icon down and stops listening for launches.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _gate.ShowRequested -= OnShowRequested;
        _coordinator.ExitRequested -= OnExitRequested;
        _coordinator.SettingsRequested -= OnSettingsRequested;
        _coordinator.RecapRequested -= OnRecapRequested;
        _services.GetRequiredService<SearchViewModel>().RecapRequested -= OnSearchRecapRequested;

        _autostart.Dispose();
        _coordinator.Dispose();
        _hotkey.Dispose();
        _tray.Dispose();
    }

    private void Start()
    {
        _coordinator.ExitRequested += OnExitRequested;
        _coordinator.SettingsRequested += OnSettingsRequested;
        _coordinator.RecapRequested += OnRecapRequested;
        _services.GetRequiredService<SearchViewModel>().RecapRequested += OnSearchRecapRequested;
        _gate.ShowRequested += OnShowRequested;

        _coordinator.Start();
        _autostart.Start();
        _gate.BeginListening();
    }

    private void OnExitRequested(object? sender, EventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Shows the settings window, or brings the one already open back to the front. A second copy
    /// of a form bound to the same view model would let the user edit two versions of one thing.
    /// </summary>
    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        if (_settingsWindow is { IsLoaded: true } existing)
        {
            existing.Activate();
            return;
        }

        _settingsWindow = _services.GetRequiredService<SettingsWindow>();
        _settingsWindow.Show();
    }

    /// <summary>
    /// The recap asked for from the search box. The box is topmost and hides when it loses focus,
    /// so it is put away first rather than left to fight the recap window for the foreground.
    /// </summary>
    private void OnSearchRecapRequested(object? sender, EventArgs e)
    {
        _coordinator.HideSearch();
        OnRecapRequested(sender, e);
    }

    /// <summary>
    /// Shows the recap window, or brings the one already open back to the front.
    /// </summary>
    private void OnRecapRequested(object? sender, EventArgs e)
    {
        if (_recapWindow is { IsLoaded: true } existing)
        {
            existing.Activate();
            return;
        }

        _recapWindow = _services.GetRequiredService<RecapWindow>();
        _recapWindow.Show();
    }

    /// <summary>
    /// A second launch reports on a thread-pool thread, so the request for the window crosses back
    /// through the dispatcher like every other piece of background work in this application.
    /// </summary>
    private void OnShowRequested(object? sender, EventArgs e) => _dispatcher.Post(_coordinator.ShowSearch);
}
