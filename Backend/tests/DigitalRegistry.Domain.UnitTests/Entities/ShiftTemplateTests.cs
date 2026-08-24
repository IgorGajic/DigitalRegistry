using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using Xunit;

namespace DigitalRegistry.Domain.UnitTests.Entities;

/// <summary>
/// The two rules a template carries: when it ends, and which days it applies to.
/// </summary>
public class ShiftTemplateTests
{
    private static ShiftTemplate Template(string start, string end) => new()
    {
        Name = "Smena",
        StartTime = TimeOnly.Parse(start),
        EndTime = TimeOnly.Parse(end)
    };

    [Theory]
    [InlineData("07:00", "15:00", false)]
    [InlineData("15:00", "23:00", false)]
    [InlineData("22:00", "06:00", true)]
    [InlineData("23:00", "23:00", true)]
    public void CrossesMidnight_IsDerivedFromTheTwoTimes(string start, string end, bool expected)
    {
        // Derived rather than stored, so a template edited from a night shift to a day shift cannot
        // keep a stale flag that would generate shifts a day long.
        Assert.Equal(expected, Template(start, end).CrossesMidnight);
    }

    [Theory]
    [InlineData("07:00", "15:00", 8)]
    [InlineData("22:00", "06:00", 8)]
    [InlineData("18:00", "02:00", 8)]
    public void Duration_CountsThroughMidnight(string start, string end, double expectedHours)
    {
        Assert.Equal(expectedHours, Template(start, end).Duration.TotalHours);
    }

    [Fact]
    public void WeekDays_MapOntoTheFrameworkDays()
    {
        Assert.True(WeekDays.Weekdays.Includes(DayOfWeek.Monday));
        Assert.True(WeekDays.Weekdays.Includes(DayOfWeek.Friday));
        Assert.False(WeekDays.Weekdays.Includes(DayOfWeek.Saturday));

        Assert.True(WeekDays.Weekend.Includes(DayOfWeek.Sunday));
        Assert.False(WeekDays.Weekend.Includes(DayOfWeek.Monday));
    }

    [Fact]
    public void CoversDate_RespectsBothTheDaysAndThePeriod()
    {
        var assignment = new ShiftAssignment
        {
            Days = WeekDays.Weekdays,
            ValidFrom = new DateOnly(2026, 9, 1),
            ValidTo = new DateOnly(2026, 9, 30)
        };

        // A Tuesday inside the period.
        Assert.True(assignment.CoversDate(new DateOnly(2026, 9, 1)));
        // A Saturday inside the period.
        Assert.False(assignment.CoversDate(new DateOnly(2026, 9, 5)));
        // A weekday outside it.
        Assert.False(assignment.CoversDate(new DateOnly(2026, 10, 1)));
        Assert.False(assignment.CoversDate(new DateOnly(2026, 8, 31)));
    }

    [Fact]
    public void CoversDate_AnOpenEndedArrangementRunsOn()
    {
        var assignment = new ShiftAssignment
        {
            Days = WeekDays.All,
            ValidFrom = new DateOnly(2026, 9, 1),
            ValidTo = null
        };

        Assert.True(assignment.CoversDate(new DateOnly(2030, 1, 1)));
    }

    [Fact]
    public void SharesAnyDayWith_IsFalseWhenTheDaysDoNotMeet()
    {
        var weekdays = new ShiftAssignment { Days = WeekDays.Weekdays, ValidFrom = new DateOnly(2026, 1, 1) };
        var weekend = new ShiftAssignment { Days = WeekDays.Weekend, ValidFrom = new DateOnly(2026, 1, 1) };

        Assert.False(weekdays.SharesAnyDayWith(weekend));
    }

    [Fact]
    public void SharesAnyDayWith_IsFalseWhenThePeriodsDoNotMeet()
    {
        var september = new ShiftAssignment
        {
            Days = WeekDays.All,
            ValidFrom = new DateOnly(2026, 9, 1),
            ValidTo = new DateOnly(2026, 9, 30)
        };

        var october = new ShiftAssignment
        {
            Days = WeekDays.All,
            ValidFrom = new DateOnly(2026, 10, 1),
            ValidTo = new DateOnly(2026, 10, 31)
        };

        Assert.False(september.SharesAnyDayWith(october));
        Assert.False(october.SharesAnyDayWith(september));
    }

    [Fact]
    public void SharesAnyDayWith_IsTrueWhenBothDaysAndPeriodsMeet()
    {
        var first = new ShiftAssignment
        {
            Days = WeekDays.Weekdays,
            ValidFrom = new DateOnly(2026, 9, 1),
            ValidTo = new DateOnly(2026, 9, 30)
        };

        var second = new ShiftAssignment
        {
            Days = WeekDays.Monday | WeekDays.Saturday,
            ValidFrom = new DateOnly(2026, 9, 15),
            ValidTo = null
        };

        Assert.True(first.SharesAnyDayWith(second));
        Assert.True(second.SharesAnyDayWith(first));
    }
}
