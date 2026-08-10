using DigitalRegistry.Domain.Common;

namespace DigitalRegistry.Domain.Events;

/// <summary>
/// Raised when an ingredient's stock falls to or below its configured threshold. Consumed by the
/// stock-exhaustion guard, which re-checks which menu items are still makeable.
/// </summary>
public sealed record IngredientLowStockDomainEvent(
    Guid IngredientId,
    string Name,
    decimal StockQuantity,
    decimal LowStockThreshold) : DomainEventBase;

/// <summary>Raised when a menu item is taken off, or put back on, the menu.</summary>
public sealed record MenuItemAvailabilityChangedDomainEvent(
    Guid MenuItemId,
    string Name,
    bool IsAvailable) : DomainEventBase;
