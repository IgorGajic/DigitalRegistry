namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// Runtime occupancy of a table. Derived from open orders and reservations rather than stored on
/// the <see cref="Entities.Table"/> row, so it can never drift out of sync with them.
/// </summary>
public enum TableStatus
{
    Available = 1,
    Reserved = 2,
    Occupied = 3,
    OutOfService = 4
}
