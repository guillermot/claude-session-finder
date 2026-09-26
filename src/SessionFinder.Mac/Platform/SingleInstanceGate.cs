using System.Net.Sockets;
using SessionFinder.Infrastructure.FileSystem;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Decides whether this process is the one that runs, and carries a second launch's intent to it.
/// </summary>
/// <remarks>
/// <para>
/// A named <see cref="Mutex"/> is what the Windows head uses and is not an option here: on Unix,
/// .NET's named mutexes are process-local, so every instance would believe it was the first. An
/// exclusive lock on a file is the portable equivalent — the operating system releases it when the
/// process ends, however it ends, which is the property that matters after a crash.
/// </para>
/// <para>
/// The second half is a Unix domain socket beside the lock file. A second launch connects to it,
/// which is the whole message: nothing is sent, and the connection arriving is what means "the user
/// asked for the window again". The socket file is deleted before it is bound, because a socket
/// left behind by a process that did not shut down cleanly would otherwise refuse the bind forever.
/// </para>
/// <para>
/// There is no counterpart to Windows' <c>AllowSetForegroundWindow</c>. macOS has no equivalent
/// permission to hand over: showing a window activates the application that owns it.
/// </para>
/// </remarks>
internal sealed class SingleInstanceGate : IDisposable
{
    private const string LockFileName = ".lock";
    private const string SocketFileName = ".show";

    private readonly FileStream? _lock;
    private readonly string _socketPath;
    private Socket? _listener;
    private CancellationTokenSource? _listening;
    private bool _disposed;

    private SingleInstanceGate(FileStream? heldLock, string socketPath)
    {
        _lock = heldLock;
        _socketPath = socketPath;
    }

    /// <summary>Raised when another launch asked the running instance to show its window.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>Whether this process is the one that should run.</summary>
    public bool IsPrimary => _lock is not null;

    /// <summary>
    /// Takes the lock if it is free.
    /// </summary>
    /// <returns>The gate, which reports whether this process is the primary instance.</returns>
    public static SingleInstanceGate Acquire()
    {
        var folder = Path.GetDirectoryName(ApplicationPaths.SettingsFile)!;

        Directory.CreateDirectory(folder);

        var socketPath = Path.Combine(folder, SocketFileName);

        try
        {
            var held = new FileStream(
                Path.Combine(folder, LockFileName),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            return new SingleInstanceGate(held, socketPath);
        }
        catch (IOException)
        {
            return new SingleInstanceGate(null, socketPath);
        }
    }

    /// <summary>
    /// Tells the instance that already holds the lock that the user asked for the window.
    /// </summary>
    /// <remarks>
    /// Failure is silent and deliberate. The only thing a second launch can do about a primary that
    /// is not listening is exit, which is what it was going to do regardless.
    /// </remarks>
    public void SignalPrimary()
    {
        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            client.Connect(new UnixDomainSocketEndPoint(_socketPath));
        }
        catch (SocketException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>
    /// Starts listening for a second launch. Only the primary instance has anything to listen for.
    /// </summary>
    public void BeginListening()
    {
        if (!IsPrimary || _listener is not null)
        {
            return;
        }

        // A socket file left by a process that did not shut down cleanly would refuse the bind.
        File.Delete(_socketPath);

        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        _listener.Listen(backlog: 1);

        _listening = new CancellationTokenSource();

        _ = AcceptAsync(_listener, _listening.Token);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _listening?.Cancel();
        _listening?.Dispose();
        _listener?.Dispose();
        _lock?.Dispose();

        if (IsPrimary)
        {
            TryDelete(_socketPath);
        }
    }

    private async Task AcceptAsync(Socket listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var connection = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);

                ShowRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
