namespace SessionFinder.Presentation.Abstractions;

/// <summary>
/// The one way a view model is allowed to reach the user interface thread.
/// </summary>
/// <remarks>
/// This port exists so that no view model ever names a dispatcher type. That keeps the debounce and
/// the ranking of results testable without a message loop, and it is what makes a second head cost
/// an adapter rather than a rewrite.
/// </remarks>
public interface IUiDispatcher
{
    /// <summary>Whether the calling thread is already the user interface thread.</summary>
    bool IsOnUiThread { get; }

    /// <summary>
    /// Queues work to run on the user interface thread and returns immediately.
    /// </summary>
    /// <param name="action">The work to run.</param>
    void Post(Action action);
}
