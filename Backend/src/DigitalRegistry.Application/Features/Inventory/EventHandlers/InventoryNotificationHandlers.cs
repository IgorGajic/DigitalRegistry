using DigitalRegistry.Application.Common.Events;
using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DigitalRegistry.Application.Features.Inventory.EventHandlers;

/// <summary>
/// Broadcasts that an item has gone off, or come back on, the menu.
/// </summary>
/// <remarks>
/// The flag itself is flipped inside the ordering transaction by
/// <see cref="Common.Services.InventoryAllocator"/>; this handler only tells connected clients about
/// it, so a kitchen display and a guest's phone both stop offering the item.
/// </remarks>
internal sealed class MenuItemAvailabilityChangedNotificationHandler(INotificationService notificationService)
    : INotificationHandler<DomainEventNotification<MenuItemAvailabilityChangedDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<MenuItemAvailabilityChangedDomainEvent> notification,
        CancellationToken cancellationToken) =>
        notificationService.MenuItemAvailabilityChangedAsync(notification.DomainEvent, cancellationToken);
}

/// <summary>
/// Records that an ingredient has reached its reorder threshold.
/// </summary>
/// <remarks>
/// Distinct from availability: crossing the threshold means "order more soon", whereas being unable
/// to cover a recipe is what actually takes an item off the menu. This is the purchasing signal, and
/// is deliberately only logged — a manager reads it from the low-stock report rather than being
/// interrupted mid-service.
/// </remarks>
internal sealed class IngredientLowStockLoggingHandler(ILogger<IngredientLowStockLoggingHandler> logger)
    : INotificationHandler<DomainEventNotification<IngredientLowStockDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<IngredientLowStockDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        logger.LogWarning(
            "Ingredient {IngredientName} is low: {StockQuantity} remaining against a threshold of "
            + "{LowStockThreshold}.",
            domainEvent.Name,
            domainEvent.StockQuantity,
            domainEvent.LowStockThreshold);

        return Task.CompletedTask;
    }
}
