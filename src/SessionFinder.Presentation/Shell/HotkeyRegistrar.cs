using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionFinder.Core.Configuration;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Presentation.Shell;

/// <summary>
/// Owns which chord the application holds: claims one at start-up, and claims a different one when
/// the setting changes, without a restart.
/// </summary>
/// <remarks>
/// <para>
/// Walking a chain of candidates means a chord another application already owns costs the user a
/// different chord rather than the tool. Running out of candidates is reported by leaving
/// <see cref="RegisteredChord"/> null, which is a state the shell renders, not an exception.
/// </para>
/// <para>
/// Reacting to a changed setting has two hazards, and both are handled here. A configuration file
/// change raises the reload callback more than once — most editors, and this application's own
/// settings window, write a file in a way that the watcher reports twice — so the reclaim is
/// debounced and, more importantly, compared before it is applied: a callback that carries the same
/// chord text that is already in force does nothing at all. That comparison is what makes the
/// repeated callbacks harmless rather than merely rare, and it is why no chord is ever released and
/// re-registered for a change that was not a change.
/// </para>
/// <para>
/// The reclaim is posted to the user interface thread because a system-wide chord belongs to the
/// thread that registered it: registering from the reload callback's thread-pool thread would claim
/// the chord for a thread with no message loop, and the key would go nowhere.
/// </para>
/// </remarks>
public sealed class HotkeyRegistrar : IDisposable
{
    private static readonly TimeSpan SettleFor = TimeSpan.FromMilliseconds(250);

    private readonly IGlobalHotkey _hotkey;
    private readonly IOptionsMonitor<ShellOptions> _options;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HotkeyRegistrar> _logger;

    private IDisposable? _subscription;
    private ITimer? _settleTimer;
    private string? _appliedPreference;
    private bool _isStarted;
    private bool _isDisposed;

    /// <summary>
    /// Builds the registrar.
    /// </summary>
    /// <param name="hotkey">The system-wide chord.</param>
    /// <param name="options">The configured chord preference, watched for changes.</param>
    /// <param name="dispatcher">The way back to the thread that owns the chord.</param>
    /// <param name="timeProvider">The clock the settling delay is measured against.</param>
    /// <param name="logger">Where the outcome of registration is recorded.</param>
    public HotkeyRegistrar(
        IGlobalHotkey hotkey,
        IOptionsMonitor<ShellOptions> options,
        IUiDispatcher dispatcher,
        TimeProvider timeProvider,
        ILogger<HotkeyRegistrar> logger)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _hotkey = hotkey;
        _options = options;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Raised on the user interface thread when the registered chord is pressed.</summary>
    public event EventHandler? Pressed;

    /// <summary>Raised after a claim, whether it succeeded or ran out of candidates.</summary>
    public event EventHandler? RegistrationChanged;

    /// <summary>
    /// The chord that is held, or <see langword="null"/> when none of the candidates could be had.
    /// </summary>
    public HotkeyChord? RegisteredChord { get; private set; }

    /// <summary>The candidates tried by the most recent claim, in the order they were tried.</summary>
    public IReadOnlyList<HotkeyChord> LastAttempt { get; private set; } = [];

    /// <summary>
    /// Claims a chord and starts following the setting. Safe to call once.
    /// </summary>
    public void Start()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;

        _hotkey.Pressed += OnHotkeyPressed;
        _subscription = _options.OnChange(OnOptionsChanged);

        Claim();
    }

    /// <summary>Releases the chord and stops following the setting.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _hotkey.Pressed -= OnHotkeyPressed;
        _subscription?.Dispose();
        _settleTimer?.Dispose();

        if (_isStarted)
        {
            _hotkey.Unregister();
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e) => Pressed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// The compare-before-apply half of the reload defence. A reload that did not change the chord
    /// is the common case — every save of the settings file raises this for every setting — and it
    /// must cost nothing.
    /// </summary>
    private void OnOptionsChanged(ShellOptions options)
    {
        if (_isDisposed || string.Equals(options.Hotkey, _appliedPreference, StringComparison.Ordinal))
        {
            return;
        }

        ScheduleReclaim();
    }

    /// <summary>
    /// The debounce half. A single save can raise the callback more than once, and each one that
    /// gets this far would otherwise release and re-register a system-wide chord.
    /// </summary>
    private void ScheduleReclaim()
    {
        _settleTimer?.Dispose();
        _settleTimer = _timeProvider.CreateTimer(
            _ => _dispatcher.Post(Reclaim),
            state: null,
            SettleFor,
            Timeout.InfiniteTimeSpan);
    }

    private void Reclaim()
    {
        if (_isDisposed)
        {
            return;
        }

        Claim();

        RegistrationChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Claim()
    {
        var preference = _options.CurrentValue.Hotkey;
        var chain = HotkeyChord.ChainFor(preference);

        _appliedPreference = preference;
        LastAttempt = chain;
        RegisteredChord = FirstAvailable(chain);
    }

    private HotkeyChord? FirstAvailable(IReadOnlyList<HotkeyChord> chain)
    {
        foreach (var chord in chain)
        {
            if (_hotkey.TryRegister(chord))
            {
                HotkeyRegistrarLog.HotkeyRegistered(_logger, chord.ToString());
                return chord;
            }

            HotkeyRegistrarLog.HotkeyUnavailable(_logger, chord.ToString());
        }

        HotkeyRegistrarLog.NoHotkeyAvailable(_logger, chain.Count);

        return null;
    }
}
