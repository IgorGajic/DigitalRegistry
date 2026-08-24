using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.ValueObjects;
using Xunit;

namespace DigitalRegistry.Domain.UnitTests.Entities;

/// <summary>
/// Confirms a shift delegates the overlap decision to <see cref="ShiftTimeRange"/> rather than
/// carrying its own copy of the rule.
/// </summary>
public class ShiftTests
{
    private static readonly DateTime Day = new(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

    private static Shift Shift(int startHour, int endHour, Guid? waiterId = null) => new()
    {
        WaiterId = waiterId ?? Guid.NewGuid(),
        StartTime = Day.AddHours(startHour),
        EndTime = Day.AddHours(endHour)
    };

    [Fact]
    public void Overlaps_DetectsAClashBetweenTwoShifts()
    {
        Assert.True(Shift(9, 17).Overlaps(Shift(16, 20)));
    }

    [Fact]
    public void Overlaps_AllowsAHandover()
    {
        Assert.False(Shift(9, 17).Overlaps(Shift(17, 21)));
    }

    [Fact]
    public void TimeRange_ReflectsTheShiftsOwnTimes()
    {
        var shift = Shift(9, 17);

        Assert.Equal(shift.StartTime, shift.TimeRange.Start);
        Assert.Equal(shift.EndTime, shift.TimeRange.End);
    }

    [Fact]
    public void Overlaps_ComparesOnlyTimes_NotWaiters()
    {
        // The entity answers a question purely about periods. Restricting the comparison to one
        // waiter is the caller's job, and the scheduling query does exactly that.
        var oneWaiter = Shift(9, 17, Guid.NewGuid());
        var anotherWaiter = Shift(10, 12, Guid.NewGuid());

        Assert.True(oneWaiter.Overlaps(anotherWaiter));
    }
}
