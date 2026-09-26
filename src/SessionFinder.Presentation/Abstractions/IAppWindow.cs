namespace SessionFinder.Presentation.Abstractions;

/// <summary>
/// The search window, as much of it as the shell logic needs to know about.
/// </summary>
/// <remarks>
/// There is deliberately no way to close it. The window is created once and shown and hidden
/// thereafter, because re-creating it would put the cost of building the visual tree between the
/// chord and the caret.
/// </remarks>
public interface IAppWindow
{
    /// <summary>Whether the window is on screen.</summary>
    bool IsVisible { get; }

    /// <summary>Shows the window, brings it to the foreground and puts the caret in the search box.</summary>
    void Show();

    /// <summary>Hides the window without destroying it.</summary>
    void Hide();
}
