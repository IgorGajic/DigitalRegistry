using DigitalRegistry.Domain.Common;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A named working period the venue runs every day — "first shift, 07:00 to 15:00".
/// </summary>
/// <remarks>
/// Times are local to the restaurant, which is how a manager thinks and speaks about them. Turning
/// them into the instants a <see cref="Shift"/> stores is the schedule generator's job, and needs the
/// venue's time zone to do it.
/// </remarks>
public class ShiftTemplate : BaseEntity, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    /// <summary>
    /// Retired templates stop being offered but keep the assignments already made from them, so a
    /// schedule generated last month still explains itself.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public ICollection<ShiftAssignment> Assignments { get; set; } = new List<ShiftAssignment>();

    /// <summary>
    /// True for a shift that ends on the following day, such as 22:00 to 06:00.
    /// </summary>
    /// <remarks>
    /// Derived from the two times rather than stored. A stored flag could contradict them — a
    /// template edited from 22:00–06:00 to 09:00–17:00 with the flag left set would generate shifts a
    /// day long — and there is nothing the flag knows that the comparison does not.
    /// </remarks>
    public bool CrossesMidnight => EndTime <= StartTime;

    /// <summary>How long the shift runs, midnight crossings included.</summary>
    public TimeSpan Duration => CrossesMidnight
        ? TimeSpan.FromDays(1) - (StartTime - EndTime)
        : EndTime - StartTime;
}
