using DigitalRegistry.Application.Features.Shifts;
using DigitalRegistry.Domain.Entities;
using Xunit;

namespace DigitalRegistry.Application.UnitTests.Shifts;

/// <summary>
/// Turning a template's local hours into stored instants.
/// </summary>
/// <remarks>
/// The conversion that, done wrong, silently puts every shift on the rota two hours out for a
/// Belgrade venue.
/// </remarks>
public class ShiftClockTests
{
    private static readonly TimeZoneInfo Belgrade = ShiftClock.ResolveTimeZone("Europe/Belgrade");

    private static ShiftTemplate Template(string start, string end) => new()
    {
        Name = "Smena",
        StartTime = TimeOnly.Parse(start),
        EndTime = TimeOnly.Parse(end)
    };

    [Fact]
    public void ResolveTimeZone_FindsAnIanaZone()
    {
        Assert.NotEqual(TimeZoneInfo.Utc, Belgrade);
    }

    [Fact]
    public void ResolveTimeZone_FallsBackToUtcForAnUnknownZone()
    {
        // A venue with a mistyped zone should still be able to build a rota, an hour or two out,
        // rather than being unable to schedule anybody at all.
        Assert.Equal(TimeZoneInfo.Utc, ShiftClock.ResolveTimeZone("Middle/Earth"));
        Assert.Equal(TimeZoneInfo.Utc, ShiftClock.ResolveTimeZone(null));
        Assert.Equal(TimeZoneInfo.Utc, ShiftClock.ResolveTimeZone("  "));
    }

    [Fact]
    public void ToUtcPeriod_ShiftsLocalHoursByTheVenueOffset()
    {
        // Belgrade is UTC+2 in summer, so a 15:00 start is stored as 13:00.
        var (startUtc, endUtc) = ShiftClock.ToUtcPeriod(
            Template("15:00", "23:00"),
            new DateOnly(2026, 7, 15),
            Belgrade);

        Assert.Equal(new DateTime(2026, 7, 15, 13, 0, 0, DateTimeKind.Utc), startUtc);
        Assert.Equal(new DateTime(2026, 7, 15, 21, 0, 0, DateTimeKind.Utc), endUtc);
    }

    [Fact]
    public void ToUtcPeriod_UsesTheWinterOffsetInWinter()
    {
        // UTC+1 in January, so the same template lands an hour later in UTC than it does in July.
        var (startUtc, _) = ShiftClock.ToUtcPeriod(
            Template("15:00", "23:00"),
            new DateOnly(2026, 1, 15),
            Belgrade);

        Assert.Equal(new DateTime(2026, 1, 15, 14, 0, 0, DateTimeKind.Utc), startUtc);
    }

    [Fact]
    public void ToUtcPeriod_CarriesANightShiftIntoTheFollowingDay()
    {
        var (startUtc, endUtc) = ShiftClock.ToUtcPeriod(
            Template("22:00", "06:00"),
            new DateOnly(2026, 7, 15),
            Belgrade);

        Assert.Equal(new DateTime(2026, 7, 15, 20, 0, 0, DateTimeKind.Utc), startUtc);
        Assert.Equal(new DateTime(2026, 7, 16, 4, 0, 0, DateTimeKind.Utc), endUtc);
        Assert.Equal(8, (endUtc - startUtc).TotalHours);
    }

    [Fact]
    public void ToUtc_SurvivesTheHourThatDoesNotExist()
    {
        // Clocks go forward at 02:00 on the last Sunday of March; 02:30 local never happens. A shift
        // starting then must still convert rather than throwing and taking the whole rota with it.
        var missingHour = new DateTime(2026, 3, 29, 2, 30, 0);

        var utc = ShiftClock.ToUtc(missingHour, Belgrade);

        Assert.Equal(DateTimeKind.Utc, utc.Kind);
        Assert.Equal(new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void ToUtc_TakesTheEarlierOfTheHourThatHappensTwice()
    {
        // Clocks go back at 03:00 on the last Sunday of October; 02:30 local happens twice. A rota
        // means the first one.
        var ambiguous = new DateTime(2026, 10, 25, 2, 30, 0);

        Assert.Equal(new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc), ShiftClock.ToUtc(ambiguous, Belgrade));
    }

    [Fact]
    public void ToUtcPeriod_IsIdentityUnderUtc()
    {
        var (startUtc, endUtc) = ShiftClock.ToUtcPeriod(
            Template("07:00", "15:00"),
            new DateOnly(2026, 7, 15),
            TimeZoneInfo.Utc);

        Assert.Equal(new DateTime(2026, 7, 15, 7, 0, 0, DateTimeKind.Utc), startUtc);
        Assert.Equal(new DateTime(2026, 7, 15, 15, 0, 0, DateTimeKind.Utc), endUtc);
    }
}
