using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DigitalRegistry.Infrastructure.RealTime.Hubs;

/// <summary>
/// The kitchen and bar display feed, mapped at <c>/hubs/kitchen</c>.
/// </summary>
/// <remarks>
/// Clients receive <c>OrderCreated</c>, <c>OrderItemUpdated</c> and
/// <c>MenuItemAvailabilityChanged</c> pushes, removing the need to poll. Restricted to staff: a
/// guest QR session must never see the whole house's order flow.
/// </remarks>
[Authorize(Roles = nameof(UserRole.Waiter) + "," + nameof(UserRole.Manager) + "," + nameof(UserRole.Owner))]
public class KitchenHub : Hub
{
    /// <summary>Event name for a newly opened order.</summary>
    public const string OrderCreated = "OrderCreated";

    /// <summary>Event name for a changed order line.</summary>
    public const string OrderItemUpdated = "OrderItemUpdated";

    /// <summary>Event name for a menu item going off or back on.</summary>
    public const string MenuItemAvailabilityChanged = "MenuItemAvailabilityChanged";
}
