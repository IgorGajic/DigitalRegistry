using DigitalRegistry.Domain.ValueObjects;
using Xunit;

namespace DigitalRegistry.Domain.UnitTests.ValueObjects;

/// <summary>
/// Tests the shift overlap rule, which is the rule the scheduling feature depends on to stop a
/// waiter being double-booked.
/// </summary>
public class ShiftTimeRangeTests
{
    private static readonly DateTime Day = new(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

    private static ShiftTimeRange Range(int startHour, int endHour) =>
        new(Day.AddHours(startHour), Day.AddHours(endHour));

    [Theory]
    // Identical periods.
    [InlineData(9, 17, 9, 17, true)]
    // The candidate starts inside the existing shift.
    [InlineData(9, 17, 16, 20, true)]
    // The candidate ends inside the existing shift.
    [InlineData(9, 17, 6, 10, true)]
    // The candidate sits entirely inside the existing shift.
    [InlineData(9, 17, 11, 12, true)]
    // The candidate entirely contains the existing shift.
    [InlineData(11, 12, 9, 17, true)]
    // Back to back: one ends exactly as the next begins. The interval is half-open, so this is not
    // an overlap — it is the ordinary shift handover.
    [InlineData(9, 17, 17, 21, false)]
    [InlineData(17, 21, 9, 17, false)]
    // Clearly disjoint, in both directions.
    [InlineData(9, 12, 14, 18, false)]
    [InlineData(14, 18, 9, 12, false)]
    public void Overlaps_ReportsWhetherTwoPeriodsShareAnyInstant(
        int existingStart,
        int existingEnd,
        int candidateStart,
        int candidateEnd,
        bool expected)
    {
        var existing = Range(existingStart, existingEnd);
        var candidate = Range(candidateStart, candidateEnd);

        Assert.Equal(expected, existing.Overlaps(candidate));
    }

    [Theory]
    [InlineData(9, 17, 14, 18)]
    [InlineData(9, 17, 17, 21)]
    [InlineData(9, 17, 11, 12)]
    public void Overlaps_IsSymmetric(int firstStart, int firstEnd, int secondStart, int secondEnd)
    {
        var first = Range(firstStart, firstEnd);
        var second = Range(secondStart, secondEnd);

        // Whichever way round the comparison is made, the answer has to agree; otherwise the
        // scheduling check would depend on the order rows came back from the database.
        Assert.Equal(first.Overlaps(second), second.Overlaps(first));
    }

    [Fact]
    public void IsChronological_IsFalseForAnInvertedPeriod()
    {
        Assert.False(Range(20, 8).IsChronological);
        Assert.True(Range(8, 20).IsChronological);
    }

    [Fact]
    public void IsChronological_IsFalseForAZeroLengthPeriod()
    {
        Assert.False(Range(9, 9).IsChronological);
    }

    [Fact]
    public void Duration_IsTheDifferenceBetweenTheEnds()
    {
        Assert.Equal(TimeSpan.FromHours(8), Range(9, 17).Duration);
    }

    [Fact]
    public void Contains_TreatsTheRangeAsHalfOpen()
    {
        var range = Range(9, 17);

        Assert.True(range.Contains(Day.AddHours(9)));
        Assert.True(range.Contains(Day.AddHours(16.5)));
        // The end instant belongs to the next shift, not this one.
        Assert.False(range.Contains(Day.AddHours(17)));
        Assert.False(range.Contains(Day.AddHours(8.9)));
    }

    [Fact]
    public void ZeroLengthRange_OverlapsNothing()
    {
        // A zero-length period contains no instants, so it cannot share one with anything. The
        // validators reject such a period outright; this pins down the behaviour if one gets through.
        Assert.False(Range(9, 9).Overlaps(Range(9, 17)));
        Assert.False(Range(9, 17).Overlaps(Range(9, 9)));
    }
}
