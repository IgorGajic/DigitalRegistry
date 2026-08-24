namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// The days of the week a recurring assignment covers.
/// </summary>
/// <remarks>
/// A flags enum rather than a collection, because "Monday to Friday" is one value a manager picks
/// rather than five rows to store. It also makes the common patterns nameable — see
/// <see cref="Weekdays"/> and <see cref="Weekend"/>.
/// <para>
/// Values follow <see cref="DayOfWeek"/> shifted into bit positions, so converting between the two is
/// a shift rather than a lookup table.
/// </para>
/// </remarks>
[Flags]
public enum WeekDays
{
    None = 0,

    Sunday = 1 << DayOfWeek.Sunday,
    Monday = 1 << DayOfWeek.Monday,
    Tuesday = 1 << DayOfWeek.Tuesday,
    Wednesday = 1 << DayOfWeek.Wednesday,
    Thursday = 1 << DayOfWeek.Thursday,
    Friday = 1 << DayOfWeek.Friday,
    Saturday = 1 << DayOfWeek.Saturday,

    /// <summary>Monday to Friday — the most common pattern a manager assigns.</summary>
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,

    Weekend = Saturday | Sunday,

    All = Weekdays | Weekend
}

/// <summary>Conversions between <see cref="WeekDays"/> and the framework's day type.</summary>
public static class WeekDaysExtensions
{
    /// <summary>The single-day flag for a calendar day.</summary>
    public static WeekDays ToFlag(this DayOfWeek day) => (WeekDays)(1 << (int)day);

    /// <summary>True when the set covers the given day.</summary>
    public static bool Includes(this WeekDays days, DayOfWeek day) => (days & day.ToFlag()) != 0;

    /// <summary>True when the set covers the given date's day of the week.</summary>
    public static bool Includes(this WeekDays days, DateOnly date) => days.Includes(date.DayOfWeek);
}
