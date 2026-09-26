using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Shell;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Claims a system-wide chord with <c>RegisterHotKey</c> and raises an event when it is pressed.
/// </summary>
/// <remarks>
/// <para>
/// The messages arrive at a message-only window — a window parented to <c>HWND_MESSAGE</c>, which
/// is never shown, never enumerated and never painted. The search window cannot serve here because
/// it is hidden most of the time and a hidden window still has to exist and pump to receive
/// <c>WM_HOTKEY</c>; tying the chord to it would make the whole feature depend on a window whose
/// entire purpose is to be absent.
/// </para>
/// <para>
/// A refused registration is reported as <see langword="false"/> rather than thrown, and the reason
/// is logged. Error 1409 is the interesting one: it means another application already owns the
/// chord, which is the case the fallback chain exists for.
/// </para>
/// </remarks>
internal sealed partial class Win32GlobalHotkey : IGlobalHotkey, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 1;
    private const int HwndMessage = -3;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWindows = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const int ErrorHotkeyAlreadyRegistered = 1409;
    private const string MessageWindowName = "ClaudeSessionFinderHotkeySink";

    private readonly ILogger<Win32GlobalHotkey> _logger;
    private readonly HwndSource _messageWindow;

    private bool _isRegistered;
    private bool _isDisposed;

    /// <summary>
    /// Creates the message-only window the chord notifications are delivered to. Must be
    /// constructed on the thread that runs the message loop.
    /// </summary>
    /// <param name="logger">Where a refused registration is recorded.</param>
    public Win32GlobalHotkey(ILogger<Win32GlobalHotkey> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _messageWindow = new HwndSource(new HwndSourceParameters(MessageWindowName)
        {
            ParentWindow = HwndMessage,
            WindowStyle = 0,
        });

        _messageWindow.AddHook(OnWindowMessage);
    }

    /// <inheritdoc />
    public event EventHandler? Pressed;

    /// <inheritdoc />
    public bool TryRegister(HotkeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        Unregister();

        if (!TryResolveVirtualKey(chord.KeyName, out var virtualKey))
        {
            Win32HotkeyLog.KeyNameNotRecognised(_logger, chord.KeyName);
            return false;
        }

        if (!RegisterHotKey(_messageWindow.Handle, HotkeyId, ToWin32Modifiers(chord.Modifiers), virtualKey))
        {
            ReportFailure(chord, Marshal.GetLastWin32Error());
            return false;
        }

        _isRegistered = true;
        return true;
    }

    /// <inheritdoc />
    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        UnregisterHotKey(_messageWindow.Handle, HotkeyId);
        _isRegistered = false;
    }

    /// <summary>Releases the chord and destroys the message-only window.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Unregister();
        _messageWindow.RemoveHook(OnWindowMessage);
        _messageWindow.Dispose();
        _isDisposed = true;
    }

    private void ReportFailure(HotkeyChord chord, int errorCode)
    {
        if (errorCode == ErrorHotkeyAlreadyRegistered)
        {
            Win32HotkeyLog.ChordAlreadyOwned(_logger, chord.ToString());
            return;
        }

        Win32HotkeyLog.RegistrationFailed(_logger, chord.ToString(), errorCode);
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmHotkey || wParam.ToInt32() != HotkeyId)
        {
            return IntPtr.Zero;
        }

        handled = true;
        Pressed?.Invoke(this, EventArgs.Empty);

        return IntPtr.Zero;
    }

    /// <summary>
    /// Turns a key name into a virtual key code using the same table WPF itself uses, so the set of
    /// names accepted in settings is exactly the set of names WPF knows.
    /// </summary>
    private static bool TryResolveVirtualKey(string keyName, out uint virtualKey)
    {
        virtualKey = 0;

        if (!Enum.TryParse<Key>(keyName, ignoreCase: true, out var key) || key == Key.None)
        {
            return false;
        }

        var resolved = KeyInterop.VirtualKeyFromKey(key);

        virtualKey = (uint)resolved;

        return resolved != 0;
    }

    /// <summary>
    /// Adds <c>MOD_NOREPEAT</c> to every chord: without it, holding the chord down repeats the
    /// notification at the keyboard repeat rate and the window flickers between shown and hidden.
    /// </summary>
    private static uint ToWin32Modifiers(HotkeyModifiers modifiers)
    {
        var value = ModNoRepeat;

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            value |= ModAlt;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            value |= ModControl;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            value |= ModShift;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            value |= ModWindows;
        }

        return value;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
