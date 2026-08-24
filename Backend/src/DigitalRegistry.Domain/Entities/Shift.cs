using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.ValueObjects;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A working period assigned to a waiter by a manager.
/// </summary>
public class Shift : BaseEntity, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public Guid WaiterId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public Guid AssignedByManagerId { get; set; }

    /// <summary>
    /// The standing arrangement this shift was generated from, or null for one entered by hand.
    /// </summary>
    /// <remarks>
    /// Lets the generator recognise its own output, so running it twice over the same weeks tops up
    /// what is missing instead of duplicating what is there. It also keeps a one-off cover shift
    /// distinguishable from the regular rota.
    /// </remarks>
    public Guid? ShiftAssignmentId { get; set; }

    public ShiftAssignment? ShiftAssignment { get; set; }

    public ApplicationUser? Waiter { get; set; }

    /// <summary>The shift's period expressed as a value object, which owns the overlap rule.</summary>
    public ShiftTimeRange TimeRange => new(StartTime, EndTime);

    /// <summary>True when this shift shares any instant with <paramref name="other"/>.</summary>
    public bool Overlaps(Shift other) => TimeRange.Overlaps(other.TimeRange);

    /// <summary>True when this shift shares any instant with the given period.</summary>
    public bool Overlaps(ShiftTimeRange other) => TimeRange.Overlaps(other);
}
