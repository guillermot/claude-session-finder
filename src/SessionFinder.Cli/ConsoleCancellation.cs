namespace SessionFinder.Cli;

/// <summary>
/// Turns the first <c>Ctrl+C</c> into a cancellation signal instead of an abrupt process kill, so
/// long-running commands get the chance to stop cleanly.
/// </summary>
internal sealed class ConsoleCancellation : IDisposable
{
    private readonly CancellationTokenSource _source = new();

    /// <summary>Subscribes to the console cancel key.</summary>
    public ConsoleCancellation()
    {
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    /// <summary>The token signalled when the user interrupts the command.</summary>
    public CancellationToken Token => _source.Token;

    /// <summary>Unsubscribes and releases the token source.</summary>
    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _source.Dispose();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _source.Cancel();
    }
}
