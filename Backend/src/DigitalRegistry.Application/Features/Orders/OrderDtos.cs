using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Orders;

/// <summary>One requested line when opening or extending an order.</summary>
public record OrderLineRequest(Guid MenuItemId, int Quantity, string? Notes = null);

/// <summary>A line on an order, priced as it was when ordered.</summary>
public record OrderItemDto(
    Guid Id,
    Guid MenuItemId,
    string MenuItemName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? Notes);

/// <summary>A tab with its lines and running total.</summary>
public record OrderDto(
    Guid Id,
    Guid TableId,
    int TableNumber,
    Guid? WaiterId,
    bool PlacedByGuest,
    OrderStatus Status,
    DateTime CreatedAt,
    decimal Total,
    IReadOnlyList<OrderItemDto> Items);

/// <summary>The record of a payment taken against an order.</summary>
public record TransactionDto(
    Guid Id,
    Guid OrderId,
    Guid ProcessedByWaiterId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    DateTime TransactionDate);
