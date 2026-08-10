using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DigitalRegistry.Infrastructure.RealTime.Hubs;

/// <summary>
/// The floor and waiter alert feed, mapped at <c>/hubs/order</c>.
/// </summary>
/// <remarks>
/// Carries the alerts that need a person to walk somewhere: a guest ordering through a table QR code
/// with no waiter involved, and a reservation checking in. Restricted to staff.
/// </remarks>
[Authorize(Roles = nameof(UserRole.Waiter) + "," + nameof(UserRole.Manager) + "," + nameof(UserRole.Owner))]
public class OrderHub : Hub
{
    /// <summary>Event name for a guest self-order placed via a table QR code.</summary>
    public const string GuestQrOrderPlaced = "GuestQrOrderPlaced";

    /// <summary>Event name for a reservation checking in.</summary>
    public const string ReservationArrivalAlert = "ReservationArrivalAlert";

    /// <summary>Event name for a settled tab, so the floor knows the table is turning over.</summary>
    public const string OrderPaid = "OrderPaid";
}
