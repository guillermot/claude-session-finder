using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SessionFinder.Presentation.Abstractions;
using SessionFinder.Presentation.Shell;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Claims a system-wide chord through Carbon's hotkey API.
/// </summary>
/// <remarks>
/// <para>
/// Carbon is the older of the two ways to do this and it is the right one. The alternative,
/// <c>NSEvent.addGlobalMonitorForEvents</c>, reads every keystroke the user types in every
/// application and needs Accessibility permission to do it — an enormous grant to ask for, and a
/// dialog in front of the first key press, in exchange for a single chord. <c>RegisterEventHotKey</c>
/// asks for nothing, delivers only the chord that was claimed, and works while another application
/// is in front.
/// </para>
/// <para>
/// It has one limitation worth stating, because it is not recoverable here. A chord the system
/// itself owns — Command with Space, for instance — is taken before any application sees it, and
/// registering it succeeds: the key simply never arrives. Win32 reports that case as a refusal, so
/// the shell's fallback chain rescues it there and cannot here. The chord that was registered is
/// reported to the user through the menu, which is the remedy.
/// </para>
/// <para>
/// The callback is a static function pointer rather than a delegate held alive by a field, so there
/// is no way for the garbage collector to move or collect the thing Carbon holds. It finds its way
/// back to the instance through a table keyed by the identifier given at registration.
/// </para>
/// </remarks>
/// <param name="logger">Where a refused chord is recorded.</param>
internal sealed partial class CarbonGlobalHotkey(ILogger<CarbonGlobalHotkey> logger)
    : IGlobalHotkey, IDisposable
{
    private const string CarbonFramework =
        "/System/Library/Frameworks/Carbon.framework/Versions/Current/Carbon";

    private const uint EventClassKeyboard = 0x6B657962;  // 'keyb'
    private const uint EventHotKeyPressed = 5;
    private const uint HotKeySignature = 0x43534658;     // 'CSFX'
    private const int Success = 0;

    private const uint CommandKeyMask = 0x0100;
    private const uint ShiftKeyMask = 0x0200;
    private const uint OptionKeyMask = 0x0800;
    private const uint ControlKeyMask = 0x1000;

    private static readonly Lock Registry = new();
    private static readonly Dictionary<uint, CarbonGlobalHotkey> ByIdentifier = [];
    private static uint _nextIdentifier = 1;
    private static IntPtr _eventHandler;

    private readonly uint _identifier = NextIdentifier();
    private IntPtr _hotKey;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler? Pressed;

    /// <inheritdoc />
    public bool TryRegister(HotkeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        ObjectDisposedException.ThrowIf(_disposed, this);

        Unregister();

        if (!CarbonKeyCodes.TryResolve(chord.KeyName, out var keyCode))
        {
            CarbonHotkeyLog.KeyNameNotRecognised(logger, chord.KeyName);

            return false;
        }

        if (!EnsureHandlerInstalled())
        {
            return false;
        }

        lock (Registry)
        {
            ByIdentifier[_identifier] = this;
        }

        var status = RegisterEventHotKey(
            keyCode,
            ToCarbonModifiers(chord.Modifiers),
            new EventHotKeyID { Signature = HotKeySignature, Id = _identifier },
            GetEventDispatcherTarget(),
            0,
            out var hotKey);

        if (status != Success)
        {
            CarbonHotkeyLog.RegistrationFailed(logger, chord.ToString(), status);

            lock (Registry)
            {
                ByIdentifier.Remove(_identifier);
            }

            return false;
        }

        _hotKey = hotKey;

        return true;
    }

    /// <inheritdoc />
    public void Unregister()
    {
        if (_hotKey == IntPtr.Zero)
        {
            return;
        }

        UnregisterEventHotKey(_hotKey);
        _hotKey = IntPtr.Zero;

        lock (Registry)
        {
            ByIdentifier.Remove(_identifier);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        _disposed = true;
    }

    /// <summary>
    /// Installs the one event handler the whole process needs. Carbon dispatches every hotkey to
    /// the same target, so a second handler would only mean hearing every chord twice.
    /// </summary>
    private bool EnsureHandlerInstalled()
    {
        lock (Registry)
        {
            if (_eventHandler != IntPtr.Zero)
            {
                return true;
            }

            var eventType = new EventTypeSpec
            {
                EventClass = EventClassKeyboard,
                EventKind = EventHotKeyPressed,
            };

            int status;

            unsafe
            {
                status = InstallEventHandler(
                    GetEventDispatcherTarget(),
                    (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, int>)&OnHotKeyEvent,
                    1,
                    ref eventType,
                    IntPtr.Zero,
                    out _eventHandler);
            }

            if (status == Success)
            {
                return true;
            }

            CarbonHotkeyLog.HandlerNotInstalled(logger, status);
            _eventHandler = IntPtr.Zero;

            return false;
        }
    }

    /// <summary>
    /// Carbon's callback. Runs on the main run loop, which Avalonia owns, so the event this raises
    /// is already on the user interface thread and the shell can act on it without marshalling.
    /// </summary>
    [UnmanagedCallersOnly]
    private static int OnHotKeyEvent(IntPtr callRef, IntPtr eventRef, IntPtr userData)
    {
        var hotKeyId = default(EventHotKeyID);

        unsafe
        {
            var status = GetEventParameter(
                eventRef,
                ParameterNameDirectObject,
                ParameterTypeEventHotKeyID,
                IntPtr.Zero,
                (uint)sizeof(EventHotKeyID),
                IntPtr.Zero,
                &hotKeyId);

            if (status != Success)
            {
                return Success;
            }
        }

        CarbonGlobalHotkey? owner;

        lock (Registry)
        {
            ByIdentifier.TryGetValue(hotKeyId.Id, out owner);
        }

        owner?.Pressed?.Invoke(owner, EventArgs.Empty);

        return Success;
    }

    private static uint ToCarbonModifiers(HotkeyModifiers modifiers)
    {
        uint mask = 0;

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            mask |= ControlKeyMask;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            mask |= OptionKeyMask;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            mask |= ShiftKeyMask;
        }

        // The Windows key is the Command key here; the enum keeps the name the settings file uses.
        if (modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            mask |= CommandKeyMask;
        }

        return mask;
    }

    private static uint NextIdentifier()
    {
        lock (Registry)
        {
            return _nextIdentifier++;
        }
    }

    private const uint ParameterNameDirectObject = 0x2D2D2D2D;   // '----'
    private const uint ParameterTypeEventHotKeyID = 0x686B6964;  // 'hkid'

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyID
    {
        public uint Signature;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint EventClass;
        public uint EventKind;
    }

    [LibraryImport(CarbonFramework)]
    private static partial IntPtr GetEventDispatcherTarget();

    [LibraryImport(CarbonFramework)]
    private static partial int RegisterEventHotKey(
        uint inHotKeyCode,
        uint inHotKeyModifiers,
        EventHotKeyID inHotKeyID,
        IntPtr inTarget,
        uint inOptions,
        out IntPtr outRef);

    [LibraryImport(CarbonFramework)]
    private static partial int UnregisterEventHotKey(IntPtr inHotKey);

    [LibraryImport(CarbonFramework)]
    private static partial int InstallEventHandler(
        IntPtr inTarget,
        IntPtr inHandler,
        uint inNumTypes,
        ref EventTypeSpec inList,
        IntPtr inUserData,
        out IntPtr outRef);

    [LibraryImport(CarbonFramework)]
    private static unsafe partial int GetEventParameter(
        IntPtr inEvent,
        uint inName,
        uint inDesiredType,
        IntPtr outActualType,
        uint inBufferSize,
        IntPtr outActualSize,
        void* outData);
}
