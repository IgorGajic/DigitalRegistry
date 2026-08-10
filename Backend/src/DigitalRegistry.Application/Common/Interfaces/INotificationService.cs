using DigitalRegistry.Domain.Events;

namespace DigitalRegistry.Application.Common.Interfaces;

/// <summary>
/// Pushes real-time updates to connected kitchen, bar and floor clients.
/// </summary>
/// <remarks>
/// The Application layer states what happened; the Infrastructure implementation decides which
/// SignalR hub and group hears about it. Handlers therefore contain no transport detail.
/// </remarks>
public interface INotificationService
{
    /// <summary>Tells the kitchen and bar displays that a new tab has been opened.</summary>
    Task OrderCreatedAsync(OrderCreatedDomainEvent domainEvent, CancellationToken cancellationToken = default);

    /// <summary>Alerts floor staff that a guest ordered via a table QR code with no waiter involved.</summary>
    Task GuestQrOrderPlacedAsync(
        GuestQrOrderPlacedDomainEvent domainEvent,
        CancellationToken cancellationToken = default);

    /// <summary>Tells the kitchen and bar displays that a line changed.</summary>
    Task OrderItemUpdatedAsync(
        OrderItemUpdatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default);

    /// <summary>Tells the floor that a tab has been settled and the table is turning over.</summary>
    Task OrderPaidAsync(OrderPaidDomainEvent domainEvent, CancellationToken cancellationToken = default);

    /// <summary>Broadcasts that an item has gone off, or come back on, the menu.</summary>
    Task MenuItemAvailabilityChangedAsync(
        MenuItemAvailabilityChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default);

    /// <summary>Alerts the floor that a guest with a reservation has arrived.</summary>
    Task ReservationArrivalAlertAsync(
        ReservationArrivalAlertDomainEvent domainEvent,
        CancellationToken cancellationToken = default);
}
