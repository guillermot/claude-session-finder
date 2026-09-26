using Avalonia.Threading;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Mac.Platform;

/// <summary>
/// Reaches the user interface thread through Avalonia's dispatcher.
/// </summary>
/// <remarks>
/// Work posted from the user interface thread runs inline rather than being queued behind whatever
/// is already waiting. The view models rely on it: a search result applied during a key press has
/// to be visible to the next line of the same method.
/// </remarks>
internal sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public bool IsOnUiThread => Dispatcher.UIThread.CheckAccess();

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsOnUiThread)
        {
            action();

            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
