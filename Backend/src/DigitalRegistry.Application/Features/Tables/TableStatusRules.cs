using System.Linq.Expressions;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Tables;

/// <summary>
/// What makes a table free, reserved or occupied, stated once.
/// </summary>
/// <remarks>
/// Two screens ask this question — the availability search a guest uses and the floor plan a waiter
/// works from — and they must agree. The predicates are exposed as expressions because EF Core has to
/// translate them into SQL and cannot call into a method to do it.
/// </remarks>
public static class TableStatusRules
{
    /// <summary>Order states that mean a tab is still running on the table.</summary>
    public static readonly Expression<Func<Order, bool>> IsOpenTab = order =>
        order.Status == OrderStatus.Open
        || order.Status == OrderStatus.InPreparation
        || order.Status == OrderStatus.Served;

    /// <summary>Reservation states that still hold a table.</summary>
    public static readonly Expression<Func<Reservation, bool>> HoldsTable = reservation =>
        reservation.Status == ReservationStatus.Pending
        || reservation.Status == ReservationStatus.Confirmed;

    /// <summary>
    /// Resolves the three signals into the one status a screen shows.
    /// </summary>
    /// <remarks>
    /// Order matters. A table out of service is reported as such whatever else is true of it, and an
    /// occupied table is reported as occupied even when also reserved — that is the condition the
    /// member of staff has to deal with first.
    /// </remarks>
    public static TableStatus Determine(bool isActive, bool isOccupied, bool isReserved)
    {
        if (!isActive)
        {
            return TableStatus.OutOfService;
        }

        return isOccupied
            ? TableStatus.Occupied
            : isReserved
                ? TableStatus.Reserved
                : TableStatus.Available;
    }
}
