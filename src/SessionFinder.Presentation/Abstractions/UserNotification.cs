namespace SessionFinder.Presentation.Abstractions;

/// <summary>
/// One thing to tell the user outside of any window.
/// </summary>
public sealed record UserNotification
{
    /// <summary>The heading, kept short enough to survive a notification balloon.</summary>
    public required string Title { get; init; }

    /// <summary>What happened, in terms the user can act on.</summary>
    public required string Message { get; init; }

    /// <summary>How the notification should be presented.</summary>
    public required NotificationSeverity Severity { get; init; }

    /// <summary>
    /// Builds an informational notice: something the user asked for finished, and nothing is wrong.
    /// </summary>
    /// <param name="title">The heading.</param>
    /// <param name="message">What happened.</param>
    /// <returns>The notification.</returns>
    public static UserNotification Information(string title, string message) => new()
    {
        Title = title,
        Message = message,
        Severity = NotificationSeverity.Information,
    };

    /// <summary>
    /// Builds a warning: something did not work, and the application carried on without it.
    /// </summary>
    /// <param name="title">The heading.</param>
    /// <param name="message">What happened and what the user can do about it.</param>
    /// <returns>The notification.</returns>
    public static UserNotification Warning(string title, string message) => new()
    {
        Title = title,
        Message = message,
        Severity = NotificationSeverity.Warning,
    };

    /// <summary>
    /// Builds an error: something failed that the user has to know about before they trust what
    /// they are looking at.
    /// </summary>
    /// <param name="title">The heading.</param>
    /// <param name="message">What happened and what the user can do about it.</param>
    /// <returns>The notification.</returns>
    public static UserNotification Error(string title, string message) => new()
    {
        Title = title,
        Message = message,
        Severity = NotificationSeverity.Error,
    };
}
