using System.Runtime.InteropServices;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Makes a second launch surface the instance that is already running instead of starting another
/// one.
/// </summary>
/// <remarks>
/// <para>
/// A mutex decides who is primary and a named event carries the request. The second process sets
/// the event and exits successfully; the first is waiting on it through the thread pool and shows
/// its window. This is preferred over broadcasting a window message: a broadcast is delivered to
/// every top-level window on the desktop and relies on a registered message identifier that any
/// other process can also register, whereas a named event is addressed to exactly one recipient.
/// </para>
/// <para>
/// Both objects live in the global namespace so that a session launched from an elevated shell and
/// one launched normally still find each other.
/// </para>
/// <para>
/// Signalling alone is not enough to put the window in front of the user. Windows only lets the
/// process that owns the foreground put another window there, so the instance that is already
/// running — which owns nothing, and has not seen the keystroke or the click that started the
/// second one — is refused, and its window appears without the keyboard. The launch that is about
/// to exit is the one holding the right, so it hands the right over before it signals. This was
/// measured rather than assumed: without the handover the window came up unfocused every time.
/// </para>
/// </remarks>
internal sealed partial class SingleInstanceGate : IDisposable
{
    private const string MutexName = @"Global\ClaudeSessionFinder.SingleInstance";
    private const string ShowEventName = @"Global\ClaudeSessionFinder.Show";
    private const int AllowAnyProcess = -1;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showRequested;

    private RegisteredWaitHandle? _registration;
    private bool _isDisposed;

    private SingleInstanceGate(Mutex mutex, EventWaitHandle showRequested, bool isPrimary)
    {
        _mutex = mutex;
        _showRequested = showRequested;
        IsPrimary = isPrimary;
    }

    /// <summary>Raised on a thread-pool thread when another launch asks for the window.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>Whether this process is the one that should keep running.</summary>
    public bool IsPrimary { get; }

    /// <summary>
    /// Works out whether this process is the first one.
    /// </summary>
    /// <returns>The gate, which the caller owns and must dispose.</returns>
    public static SingleInstanceGate Acquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var isPrimary);
        var showRequested = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ShowEventName);

        return new SingleInstanceGate(mutex, showRequested, isPrimary);
    }

    /// <summary>
    /// Starts listening for the launches that come after this one. Only the primary instance does
    /// this.
    /// </summary>
    public void BeginListening()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _registration ??= ThreadPool.RegisterWaitForSingleObject(
            _showRequested,
            (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>
    /// Asks the instance that is already running to show its window. Only a secondary instance
    /// does this, immediately before exiting.
    /// </summary>
    public void SignalPrimary()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        AllowSetForegroundWindow(AllowAnyProcess);

        _showRequested.Set();
    }

    /// <summary>Stops listening and releases the mutex and the event.</summary>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _registration?.Unregister(waitObject: null);

        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _showRequested.Dispose();
    }

    /// <summary>
    /// Passes this process's right to set the foreground window to whichever process takes it next.
    /// The identifier of the instance already running is not known here, and the window it is being
    /// granted for is the one this launch was asking for anyway.
    /// </summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
