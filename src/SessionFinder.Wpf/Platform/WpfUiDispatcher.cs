using System.Windows.Threading;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Wpf.Platform;

/// <summary>
/// Marshals work onto the WPF dispatcher.
/// </summary>
/// <remarks>
/// Work already on the user interface thread runs inline rather than being queued. Queuing it would
/// mean a keystroke and the results it produced could be applied in either order, which is the one
/// thing this adapter exists to prevent.
/// </remarks>
/// <param name="dispatcher">The dispatcher of the thread that owns the windows.</param>
internal sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    /// <inheritdoc />
    public bool IsOnUiThread => dispatcher.CheckAccess();

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }
}
