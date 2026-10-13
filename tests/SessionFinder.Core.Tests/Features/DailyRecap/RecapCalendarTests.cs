using SessionFinder.Core.Features.DailyRecap;
using static SessionFinder.Core.Tests.Features.DailyRecap.RecapFixtures;

namespace SessionFinder.Core.Tests.Features.DailyRecap;

public sealed class RecapCalendarTests
{
    private readonly RecapCalendar _calendar = new(Zone, dayStartHour: 4);

    [Fact]
    public void DayOf_BeforeTheStartHour_BelongsToThePreviousDay()
    {
        _calendar.DayOf(At(10, 3, 3, 59)).Should().Be(Day(10, 2));
    }

    [Fact]
    public void DayOf_AtTheStartHour_BelongsToThatDay()
    {
        _calendar.DayOf(At(10, 3, 4)).Should().Be(Day(10, 3));
    }

    [Fact]
    public void DayOf_AnInstantGivenInAnotherZone_IsJudgedInTheUsersZone()
    {
        var utc = new DateTimeOffset(2026, 10, 3, 6, 59, 0, TimeSpan.Zero);

        _calendar.DayOf(utc).Should().Be(Day(10, 2));
    }

    [Fact]
    public void StartOf_ADay_IsTheStartHourInLocalTime()
    {
        _calendar.StartOf(Day(10, 2)).Should().Be(At(10, 2, 4));
    }

    [Fact]
    public void StartOf_AStartHourInsideASkippedHour_MovesToTheFirstInstantThatExists()
    {
        var newYork = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var calendar = new RecapCalendar(newYork, dayStartHour: 2);

        var start = calendar.StartOf(new DateOnly(2026, 3, 8));

        start.Should().Be(new DateTimeOffset(2026, 3, 8, 3, 0, 0, TimeSpan.FromHours(-4)));
    }

    [Fact]
    public void Constructor_AnOutOfRangeHour_IsClampedRatherThanThrowing()
    {
        var calendar = new RecapCalendar(Zone, dayStartHour: 30);

        calendar.StartOf(Day(10, 2)).Should().Be(At(10, 2, 23));
    }
}
