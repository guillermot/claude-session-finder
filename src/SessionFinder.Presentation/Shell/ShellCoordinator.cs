using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Presentation.Shell;

/// <summary>
/// Wires the two ways into the application — the system-wide chord and the tray menu — to the one
/// window, and owns what the menu entries mean.
/// </summary>
/// <remarks>
/// <para>
/// The chord is the feature; the tray menu is the guarantee. A chord another application already
/// owns costs the user a different chord rather than the tool, and a chain that runs out leaves the
/// menu working and says out loud what failed. There is no path here that ends in a launcher that
/// silently does nothing, which is the failure this type exists to rule out.
/// </para>
/// <para>
/// The chord that was actually registered goes into the tray tooltip, because "which key opens
/// this" is a question the user will have and the answer is not necessarily the one they
/// configured. It is rewritten whenever the registration changes, so a chord changed in settings
/// is reflected without a restart.
/// </para>
/// <para>
/// The two menu entries that do work rather than show a window run through the command guard, for
/// the same reason every other user-initiated action does: it is the one place a failure becomes
/// something the user can read.
/// </para>
/// </remarks>
public sealed class ShellCoordinator : IDisposable
{
    private const string ProductName = "Claude Session Finder";
    private const string HotkeyFailureTitle = "No hotkey available";
    private const string RebuildIndexAction = "rebuild index";
    private const string RebuildFailureTitle = "Could not rebuild the index";
    private const string OpenLogFolderAction = "open log folder";
    private const string OpenLogFolderFailureTitle = "Could not open the log folder";

    private readonly HotkeyRegistrar _registrar;
    private readonly IAppWindow _window;
    private readonly ITrayIcon _tray;
    private readonly IUserNotifier _notifier;
    private readonly IndexMaintenance _maintenance;
    private readonly AppCommandGuard _guard;
    private readonly CancellationTokenSource _lifetime = new();

    private bool _isStarted;

    /// <summary>
    /// Builds the coordinator.
    /// </summary>
    /// <param name="registrar">Owns which system-wide chord is held.</param>
    /// <param name="window">The search window.</param>
    /// <param name="tray">The notification-area icon and its menu.</param>
    /// <param name="notifier">How a failure is reported when there is no window to report it in.</param>
    /// <param name="maintenance">What the menu entries that act on the index do.</param>
    /// <param name="guard">The seam every user-initiated action is reported through.</param>
    public ShellCoordinator(
        HotkeyRegistrar registrar,
        IAppWindow window,
        ITrayIcon tray,
        IUserNotifier notifier,
        IndexMaintenance maintenance,
        AppCommandGuard guard)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(tray);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(maintenance);
        ArgumentNullException.ThrowIfNull(guard);

        _registrar = registrar;
        _window = window;
        _tray = tray;
        _notifier = notifier;
        _maintenance = maintenance;
        _guard = guard;
    }

    /// <summary>Raised when the user asks, from the tray menu, for the settings window.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>Raised when the user asks, from the tray menu, for the application to end.</summary>
    public event EventHandler? ExitRequested;

    /// <summary>
    /// The chord that is registered, or <see langword="null"/> when none of the candidates could be.
    /// </summary>
    public HotkeyChord? RegisteredChord => _registrar.RegisteredChord;

    /// <summary>
    /// Puts the tray icon up, claims a chord and starts listening. Safe to call once.
    /// </summary>
    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;

        _registrar.Pressed += OnHotkeyPressed;
        _registrar.RegistrationChanged += OnRegistrationChanged;

        _tray.SearchRequested += OnSearchRequested;
        _tray.RebuildIndexRequested += OnRebuildIndexRequested;
        _tray.SettingsRequested += OnSettingsRequested;
        _tray.LogFolderRequested += OnLogFolderRequested;
        _tray.ExitRequested += OnExitRequested;

        _tray.Show();

        _registrar.Start();

        ReportRegistration();
    }

    /// <summary>
    /// Shows the window if it is hidden and hides it if it is showing, which is what pressing the
    /// chord a second time has to do.
    /// </summary>
    public void Toggle()
    {
        if (_window.IsVisible)
        {
            _window.Hide();
            return;
        }

        _window.Show();
    }

    /// <summary>Brings the search window up whether or not it was already showing.</summary>
    public void ShowSearch() => _window.Show();

    /// <summary>Hides the search window.</summary>
    public void HideSearch() => _window.Hide();

    /// <summary>Releases the chord and stops listening.</summary>
    public void Dispose()
    {
        if (!_isStarted)
        {
            return;
        }

        _isStarted = false;

        _registrar.Pressed -= OnHotkeyPressed;
        _registrar.RegistrationChanged -= OnRegistrationChanged;

        _tray.SearchRequested -= OnSearchRequested;
        _tray.RebuildIndexRequested -= OnRebuildIndexRequested;
        _tray.SettingsRequested -= OnSettingsRequested;
        _tray.LogFolderRequested -= OnLogFolderRequested;
        _tray.ExitRequested -= OnExitRequested;

        _lifetime.Cancel();
        _registrar.Dispose();
        _lifetime.Dispose();
    }

    /// <summary>
    /// Puts the chord in the tooltip, and says out loud when there is none. Running out of
    /// candidates is survivable — the menu opens the same window — but it must never be silent.
    /// </summary>
    private void ReportRegistration()
    {
        _tray.SetTooltip(BuildTooltip(_registrar.RegisteredChord));

        if (_registrar.RegisteredChord is not null)
        {
            return;
        }

        _notifier.Notify(UserNotification.Warning(HotkeyFailureTitle, DescribeFailure(_registrar.LastAttempt)));
    }

    private static string DescribeFailure(IReadOnlyList<HotkeyChord> chain) =>
        $"Another application already owns {string.Join(", ", chain.Select(chord => chord.ToString()))}. "
        + "Open the search box from this menu, or set a free chord in settings.";

    private static string BuildTooltip(HotkeyChord? chord) =>
        chord is null
            ? $"{ProductName} — no hotkey; use this menu"
            : $"{ProductName} — {chord}";

    private void OnHotkeyPressed(object? sender, EventArgs e) => Toggle();

    private void OnRegistrationChanged(object? sender, EventArgs e) => ReportRegistration();

    private void OnSearchRequested(object? sender, EventArgs e) => ShowSearch();

    private void OnSettingsRequested(object? sender, EventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnExitRequested(object? sender, EventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// A menu entry is a click, not an awaitable, so the work is started and left to the guard.
    /// Nothing can escape it, which is the whole reason it is allowed to be started this way.
    /// </summary>
    private void OnRebuildIndexRequested(object? sender, EventArgs e) => _ = _guard.RunAsync(
        RebuildIndexAction,
        RebuildFailureTitle,
        () => _maintenance.RebuildIndexAsync(_lifetime.Token));

    private void OnLogFolderRequested(object? sender, EventArgs e) => _ = _guard.RunAsync(
        OpenLogFolderAction,
        OpenLogFolderFailureTitle,
        () => _maintenance.OpenLogFolderAsync(_lifetime.Token));
}
