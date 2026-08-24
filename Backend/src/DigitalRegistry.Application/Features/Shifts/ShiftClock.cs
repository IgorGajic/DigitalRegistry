using DigitalRegistry.Domain.Entities;

namespace DigitalRegistry.Application.Features.Shifts;

/// <summary>
/// Turns a template's local working hours into the instants a shift is stored as.
/// </summary>
/// <remarks>
/// A manager writes "second shift, 15:00 to 23:00" meaning the clock on the wall. Shifts are stored in
/// UTC, so generating a rota is a conversion, and doing it wrong is not subtle: for a Belgrade venue,
/// treating 15:00 local as 15:00 UTC puts every shift on the schedule two hours late.
/// <para>
/// The venue's own time zone is used, taken from <see cref="Restaurant.TimeZoneId"/> — not the
/// server's, which says nothing about where the restaurant is.
/// </para>
/// </remarks>
public static class ShiftClock
{
    /// <summary>
    /// Resolves a venue's time zone, falling back to UTC when the identifier is not one this machine
    /// recognises.
    /// </summary>
    /// <remarks>
    /// Falling back rather than failing: a venue with a mistyped zone should still be able to build a
    /// rota, an hour or two out, rather than being unable to schedule anybody at all.
    /// </remarks>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Works out when a shift generated from a template on a given date starts and ends, in UTC.
    /// </summary>
    /// <param name="date">The day the shift belongs to, in the venue's local calendar.</param>
    public static (DateTime StartUtc, DateTime EndUtc) ToUtcPeriod(
        ShiftTemplate template,
        DateOnly date,
        TimeZoneInfo timeZone)
    {
        var localStart = date.ToDateTime(template.StartTime);

        // A shift ending at or before it starts runs past midnight into the following day.
        var localEnd = (template.CrossesMidnight ? date.AddDays(1) : date).ToDateTime(template.EndTime);

        return (ToUtc(localStart, timeZone), ToUtc(localEnd, timeZone));
    }

    /// <summary>
    /// Converts a local wall-clock time to UTC, coping with the two awkward hours a year.
    /// </summary>
    /// <remarks>
    /// When the clocks go forward an hour does not exist locally, and a template starting inside it
    /// would otherwise throw. That hour is pushed forward by the gap, which is what actually happens
    /// to a shift starting then. When they go back an hour happens twice; the earlier one is taken,
    /// which is the one a rota means.
    /// </remarks>
    public static DateTime ToUtc(DateTime localTime, TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(unspecified))
        {
            var adjustment = timeZone.GetAdjustmentRules()
                .FirstOrDefault(rule => unspecified >= rule.DateStart && unspecified <= rule.DateEnd);

            var gap = adjustment?.DaylightDelta ?? TimeSpan.FromHours(1);

            return TimeZoneInfo.ConvertTimeToUtc(unspecified.Add(gap), timeZone);
        }

        if (timeZone.IsAmbiguousTime(unspecified))
        {
            // ConvertTimeToUtc would pick standard time here, which is the second of the two. A rota
            // means the first: staff arrive when the clock first reads that hour, before it goes back.
            // The larger offset is the daylight one, and subtracting it gives the earlier instant.
            var earliestOffset = timeZone.GetAmbiguousTimeOffsets(unspecified).Max();

            return DateTime.SpecifyKind(unspecified - earliestOffset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    }
}
