namespace SessionFinder.Presentation.Abstractions;

/// <summary>
/// How much attention a notification is asking for.
/// </summary>
public enum NotificationSeverity
{
    /// <summary>Something worth knowing that changes nothing.</summary>
    Information = 0,

    /// <summary>Something did not work, and the application carried on in a reduced form.</summary>
    Warning = 1,

    /// <summary>Something failed that the user has to deal with.</summary>
    Error = 2,
}
