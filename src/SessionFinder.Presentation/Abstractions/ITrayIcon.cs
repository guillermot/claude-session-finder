namespace SessionFinder.Presentation.Abstractions;

/// <summary>
/// The notification-area icon and its menu, which is the only part of the application that is
/// always reachable.
/// </summary>
/// <remarks>
/// It matters that this is not merely decorative: when the chord cannot be registered, the menu is
/// the whole user interface, so the shell keeps it working rather than treating a failed chord as a
/// failed start.
/// </remarks>
public interface ITrayIcon
{
    /// <summary>Raised when the user picks the menu entry that opens the search box.</summary>
    event EventHandler? SearchRequested;

    /// <summary>Raised when the user picks the menu entry that shows the daily recap.</summary>
    event EventHandler? RecapRequested;

    /// <summary>Raised when the user picks the menu entry that reads every transcript again.</summary>
    event EventHandler? RebuildIndexRequested;

    /// <summary>Raised when the user picks the menu entry that opens the settings window.</summary>
    event EventHandler? SettingsRequested;

    /// <summary>Raised when the user picks the menu entry that shows the log folder.</summary>
    event EventHandler? LogFolderRequested;

    /// <summary>Raised when the user picks the menu entry that ends the application.</summary>
    event EventHandler? ExitRequested;

    /// <summary>Puts the icon in the notification area.</summary>
    void Show();

    /// <summary>
    /// Replaces the hover text, which is where the chord that was actually registered is reported.
    /// </summary>
    /// <param name="text">The text to show on hover.</param>
    void SetTooltip(string text);
}
