namespace SessionFinder.Presentation.Abstractions;

/// <summary>
/// Tells the user something when there is no window in front of them to tell it in.
/// </summary>
public interface IUserNotifier
{
    /// <summary>
    /// Shows one notification.
    /// </summary>
    /// <param name="notification">What to say and how loudly.</param>
    void Notify(UserNotification notification);
}
