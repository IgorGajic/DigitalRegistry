using DigitalRegistry.Application.Common.Events;
using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Domain.Events;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.EventHandlers;

/// <summary>
/// Pushes a new tab to the kitchen and bar displays.
/// </summary>
/// <remarks>
/// These handlers run after the order's transaction has committed, so a display is never told about
/// an order that was subsequently rolled back.
/// </remarks>
internal sealed class OrderCreatedNotificationHandler(INotificationService notificationService)
    : INotificationHandler<DomainEventNotification<OrderCreatedDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<OrderCreatedDomainEvent> notification,
        CancellationToken cancellationToken) =>
        notificationService.OrderCreatedAsync(notification.DomainEvent, cancellationToken);
}

/// <summary>
/// Alerts the floor that a guest ordered through a table QR code with no waiter involved.
/// </summary>
internal sealed class GuestQrOrderPlacedNotificationHandler(INotificationService notificationService)
    : INotificationHandler<DomainEventNotification<GuestQrOrderPlacedDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<GuestQrOrderPlacedDomainEvent> notification,
        CancellationToken cancellationToken) =>
        notificationService.GuestQrOrderPlacedAsync(notification.DomainEvent, cancellationToken);
}

/// <summary>Pushes a changed order line to the kitchen and bar displays.</summary>
internal sealed class OrderItemUpdatedNotificationHandler(INotificationService notificationService)
    : INotificationHandler<DomainEventNotification<OrderItemUpdatedDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<OrderItemUpdatedDomainEvent> notification,
        CancellationToken cancellationToken) =>
        notificationService.OrderItemUpdatedAsync(notification.DomainEvent, cancellationToken);
}

/// <summary>Tells the floor that a tab has been settled and the table is turning over.</summary>
internal sealed class OrderPaidNotificationHandler(INotificationService notificationService)
    : INotificationHandler<DomainEventNotification<OrderPaidDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<OrderPaidDomainEvent> notification,
        CancellationToken cancellationToken) =>
        notificationService.OrderPaidAsync(notification.DomainEvent, cancellationToken);
}
