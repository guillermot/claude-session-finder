namespace SessionFinder.Core.Features.DailyRecap;

/// <summary>
/// Decides which working day an instant belongs to, and where a working day starts.
/// </summary>
/// <remarks>
/// <para>
/// A working day is a local calendar day shifted by the configured start hour, so that a session
/// that runs on past midnight is reported as part of the day it began in. That is what a person
/// means by "yesterday" at a stand-up, and it is not what the calendar means.
/// </para>
/// <para>
/// The start of a day is resolved against the time zone's own rules rather than by adding hours to
/// midnight, so a day that contains a daylight saving transition is still exactly the span between
/// two consecutive start times. A start hour that falls inside a skipped hour moves forward to the
/// first instant that exists; one that falls inside a repeated hour takes the earlier instant.
/// </para>
/// </remarks>
public sealed class RecapCalendar
{
    private const int LastHourOfDay = 23;
    private static readonly TimeSpan GapStep = TimeSpan.FromMinutes(15);

    private readonly TimeZoneInfo _timeZone;
    private readonly int _dayStartHour;

    /// <summary>
    /// Builds a calendar for one time zone and start hour.
    /// </summary>
    /// <param name="timeZone">The zone the user lives in.</param>
    /// <param name="dayStartHour">The local hour a working day starts at, clamped to a valid hour.</param>
    public RecapCalendar(TimeZoneInfo timeZone, int dayStartHour)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        _timeZone = timeZone;
        _dayStartHour = Math.Clamp(dayStartHour, 0, LastHourOfDay);
    }

    /// <summary>
    /// Names the working day an instant falls in.
    /// </summary>
    /// <param name="instant">Any instant.</param>
    /// <returns>The working day.</returns>
    public DateOnly DayOf(DateTimeOffset instant) =>
        DateOnly.FromDateTime(ToLocal(instant).DateTime.AddHours(-_dayStartHour));

    /// <summary>
    /// Finds the instant a working day starts at.
    /// </summary>
    /// <param name="day">The working day.</param>
    /// <returns>The first instant that belongs to it.</returns>
    public DateTimeOffset StartOf(DateOnly day)
    {
        var local = day.ToDateTime(new TimeOnly(_dayStartHour, 0), DateTimeKind.Unspecified);

        while (_timeZone.IsInvalidTime(local))
        {
            local = local.Add(GapStep);
        }

        var offset = _timeZone.IsAmbiguousTime(local)
            ? _timeZone.GetAmbiguousTimeOffsets(local).Max()
            : _timeZone.GetUtcOffset(local);

        return new DateTimeOffset(local, offset);
    }

    /// <summary>
    /// Restates an instant in the user's own time zone, which is the form every time on the recap
    /// is shown in.
    /// </summary>
    /// <param name="instant">Any instant.</param>
    /// <returns>The same instant, carrying the local offset.</returns>
    public DateTimeOffset ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, _timeZone);
}
