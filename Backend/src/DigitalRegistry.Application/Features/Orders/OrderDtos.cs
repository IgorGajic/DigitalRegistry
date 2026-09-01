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

/// <summary>
/// One line of the bill list: a tab as it appears on the "recent bills" screen.
/// </summary>
/// <remarks>
/// Deliberately without the order's lines. The list exists to find a bill again — by table, by hour,
/// by amount — and pulling every line of every order to render a table of totals would fetch far
/// more than the screen shows. The lines arrive with the receipt, once one bill has been chosen.
/// </remarks>
/// <param name="PaidAtUtc">When payment was taken, or null while the tab is still running.</param>
/// <param name="IsReversed">
/// True once a settled bill has been backed out. Kept separate from <paramref name="Status"/> for
/// the client, which greys such rows rather than offering to reverse them a second time.
/// </param>
public record OrderSummaryDto(
    Guid Id,
    string Number,
    Guid TableId,
    int TableNumber,
    OrderStatus Status,
    bool PlacedByGuest,
    string? ServedBy,
    DateTime CreatedAt,
    DateTime? PaidAtUtc,
    PaymentMethod? PaymentMethod,
    int ItemCount,
    decimal Total,
    bool IsReversed);
