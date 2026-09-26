using System.Globalization;

namespace SessionFinder.Presentation.Search;

/// <summary>
/// Renders when a session was last touched, the way a person would say it.
/// </summary>
/// <remarks>
/// Recent work is described relative to now, because "12 minutes ago" answers "is this the session
/// I was just in" and a timestamp does not. Anything older than a week gets a date instead, because
/// past that point the relative form stops being easier to read than the thing it replaced.
/// </remarks>
public static class LastActivityText
{
    private const string UnknownText = "unknown";
    private const string JustNowText = "just now";
    private const string DateFormat = "yyyy-MM-dd";
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;
    private const int DaysShownRelatively = 7;

    /// <summary>
    /// Describes a moment relative to another one.
    /// </summary>
    /// <param name="lastActivity">When the session was last written to, if it is known.</param>
    /// <param name="now">The moment to describe it against.</param>
    /// <returns>A short phrase, or a date for anything over a week old.</returns>
    public static string For(DateTimeOffset? lastActivity, DateTimeOffset now)
    {
        if (lastActivity is not { } moment)
        {
            return UnknownText;
        }

        var age = now - moment;

        if (age < TimeSpan.Zero || age >= TimeSpan.FromDays(DaysShownRelatively))
        {
            return moment.ToLocalTime().ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        return Describe(age);
    }

    private static string Describe(TimeSpan age)
    {
        var minutes = (int)age.TotalMinutes;

        if (minutes < 1)
        {
            return JustNowText;
        }

        if (minutes < MinutesPerHour)
        {
            return Plural(minutes, "minute");
        }

        var hours = (int)age.TotalHours;

        return hours < HoursPerDay ? Plural(hours, "hour") : Plural((int)age.TotalDays, "day");
    }

    private static string Plural(int count, string unit) => string.Create(
        CultureInfo.InvariantCulture,
        $"{count} {unit}{(count == 1 ? string.Empty : "s")} ago");
}
