using Microsoft.Extensions.Logging;
using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Presentation.Shell;

/// <summary>
/// Writes every notification to the log on its way to the user.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the visible channel is not reliable. A notification balloon is suppressed
/// whenever notifications are turned off for the application, while focus assist is on, or by
/// policy — and it is suppressed silently, so the application cannot tell the difference between a
/// message that was read and one that was never drawn. Measured on the machine this was built on,
/// balloons produced nothing at all.
/// </para>
/// <para>
/// A decorator rather than a call inside each notifier: there is one place notifications pass
/// through, and wrapping it means no future notifier has to remember. The message text is the
/// application's own prose, never anything from a transcript, so writing it to a file is safe.
/// </para>
/// </remarks>
/// <param name="inner">The notifier that actually shows the message.</param>
/// <param name="logger">Where every notification is recorded.</param>
public sealed class LoggingUserNotifier(IUserNotifier inner, ILogger<LoggingUserNotifier> logger) : IUserNotifier
{
    /// <inheritdoc />
    public void Notify(UserNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        Record(notification);

        inner.Notify(notification);
    }

    private void Record(UserNotification notification)
    {
        switch (notification.Severity)
        {
            case NotificationSeverity.Error:
                NotificationLog.ErrorShown(logger, notification.Title, notification.Message);
                break;
            case NotificationSeverity.Warning:
                NotificationLog.WarningShown(logger, notification.Title, notification.Message);
                break;
            default:
                NotificationLog.NoticeShown(logger, notification.Title, notification.Message);
                break;
        }
    }
}
