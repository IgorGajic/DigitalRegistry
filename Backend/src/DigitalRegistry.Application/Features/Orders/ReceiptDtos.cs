using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Orders;

/// <summary>
/// A bill as printed.
/// </summary>
/// <remarks>
/// Not a fiscal receipt: no tax authority has seen it and no fiscal device produced it. Everything is
/// as the order captured it, so reprinting a bill from last year shows last year's prices.
/// </remarks>
/// <param name="Number">
/// A short human-readable reference derived from the order id, for a guest querying their bill. Not
/// a sequential invoice number — the system issues none.
/// </param>
/// <param name="IsReversed">
/// True once the bill has been reversed. Printed on the copy so a reversed bill cannot be passed off
/// as a valid one.
/// </param>
public record ReceiptDto(
    Guid OrderId,
    string Number,
    string RestaurantName,
    string? RestaurantAddress,
    string? RestaurantPhone,
    string CurrencyCode,
    int TableNumber,
    string? ServedBy,
    DateTime OpenedAtUtc,
    DateTime? PaidAtUtc,
    PaymentMethod? PaymentMethod,
    OrderStatus Status,
    bool IsReversed,
    decimal Total,
    IReadOnlyList<ReceiptLineDto> Lines);

/// <summary>One line on a printed bill.</summary>
public record ReceiptLineDto(
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? Notes);
