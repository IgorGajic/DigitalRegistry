using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A standing arrangement: this waiter works this shift on these days, over this period.
/// </summary>
/// <remarks>
/// "Marko works second shift Monday to Friday through September" is one of these. It is a rule, not a
/// rota — the concrete <see cref="Shift"/> rows the till and the payroll read are produced from it by
/// the schedule generator, and can then be adjusted individually without disturbing the arrangement
/// they came from.
/// </remarks>
public class ShiftAssignment : BaseEntity, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public Guid WaiterId { get; set; }

    public ApplicationUser? Waiter { get; set; }

    public Guid ShiftTemplateId { get; set; }

    public ShiftTemplate? ShiftTemplate { get; set; }

    /// <summary>Which days of the week the arrangement covers.</summary>
    public WeekDays Days { get; set; }

    public DateOnly ValidFrom { get; set; }

    /// <summary>When the arrangement ends, or null for one that runs until cancelled.</summary>
    public DateOnly? ValidTo { get; set; }

    public Guid AssignedByManagerId { get; set; }

    /// <summary>True when the arrangement covers the given date.</summary>
    public bool CoversDate(DateOnly date) =>
        date >= ValidFrom
        && (ValidTo is null || date <= ValidTo)
        && Days.Includes(date);

    /// <summary>True when two arrangements could ever produce shifts on the same day.</summary>
    /// <remarks>
    /// Used to stop a manager assigning one waiter to two shifts that clash before any schedule is
    /// generated, rather than letting the clash surface weeks later as a generation failure.
    /// </remarks>
    public bool SharesAnyDayWith(ShiftAssignment other)
    {
        if ((Days & other.Days) == WeekDays.None)
        {
            return false;
        }

        var startsAfterOtherEnds = other.ValidTo is { } otherEnd && ValidFrom > otherEnd;
        var endsBeforeOtherStarts = ValidTo is { } end && end < other.ValidFrom;

        return !startsAfterOtherEnds && !endsBeforeOtherStarts;
    }
}
