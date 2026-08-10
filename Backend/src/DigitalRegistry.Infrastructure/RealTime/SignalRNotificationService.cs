using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Infrastructure.RealTime.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DigitalRegistry.Infrastructure.RealTime;

/// <summary>
/// Pushes real-time events to the kitchen and floor hubs.
/// </summary>
/// <remarks>
/// Broadcasts to all clients of the relevant hub, which is what a shared kitchen or bar screen
/// wants. The domain event records are sent as the payload, so the wire format follows the domain
/// rather than being restated here.
/// </remarks>
internal sealed class SignalRNotificationService(
    IHubContext<KitchenHub> kitchenHub,
    IHubContext<OrderHub> orderHub,
    ILogger<SignalRNotificationService> logger) : INotificationService
{
    public Task OrderCreatedAsync(
        OrderCreatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default) =>
        SendAsync(kitchenHub, KitchenHub.OrderCreated, domainEvent, cancellationToken);

    public Task OrderItemUpdatedAsync(
        OrderItemUpdatedDomainEvent domainEvent,
        CancellationToken cancellationToken = default) =>
        SendAsync(kitchenHub, KitchenHub.OrderItemUpdated, domainEvent, cancellationToken);

    public Task MenuItemAvailabilityChangedAsync(
        MenuItemAvailabilityChangedDomainEvent domainEvent,
        CancellationToken cancellationToken = default) =>
        SendAsync(kitchenHub, KitchenHub.MenuItemAvailabilityChanged, domainEvent, cancellationToken);

    public Task GuestQrOrderPlacedAsync(
        GuestQrOrderPlacedDomainEvent domainEvent,
        CancellationToken cancellationToken = default) =>
        SendAsync(orderHub, OrderHub.GuestQrOrderPlaced, domainEvent, cancellationToken);

    public Task ReservationArrivalAlertAsync(
        ReservationArrivalAlertDomainEvent domainEvent,
        CancellationToken cancellationToken = default) =>
        SendAsync(orderHub, OrderHub.ReservationArrivalAlert, domainEvent, cancellationToken);

    public Task OrderPaidAsync(OrderPaidDomainEvent domainEvent, CancellationToken cancellationToken = default) =>
        SendAsync(orderHub, OrderHub.OrderPaid, domainEvent, cancellationToken);

    private async Task SendAsync<THub, TPayload>(
        IHubContext<THub> hub,
        string eventName,
        TPayload payload,
        CancellationToken cancellationToken)
        where THub : Hub
    {
        try
        {
            await hub.Clients.All.SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception)
        {
            // A broken display must never fail the command that has already been committed.
            logger.LogWarning(
                exception,
                "Failed to broadcast {EventName} on {HubName}.",
                eventName,
                typeof(THub).Name);
        }
    }
}
