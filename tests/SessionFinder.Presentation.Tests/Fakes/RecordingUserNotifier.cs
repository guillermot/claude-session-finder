using SessionFinder.Presentation.Abstractions;

namespace SessionFinder.Presentation.Tests.Fakes;

internal sealed class RecordingUserNotifier : IUserNotifier
{
    private readonly List<UserNotification> _notifications = [];

    public IReadOnlyList<UserNotification> Notifications => _notifications;

    public void Notify(UserNotification notification) => _notifications.Add(notification);
}
