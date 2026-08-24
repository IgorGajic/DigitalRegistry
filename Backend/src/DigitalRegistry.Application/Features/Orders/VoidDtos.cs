using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Orders;

/// <summary>What a void did, returned so the till can confirm it to the waiter.</summary>
/// <param name="Amount">What came off the bill.</param>
/// <param name="RemainingTotal">What the tab now stands at; zero once the whole order has gone.</param>
public record VoidResultDto(
    Guid VoidRecordId,
    Guid OrderId,
    VoidType Type,
    string? ItemName,
    int Quantity,
    decimal Amount,
    decimal RemainingTotal,
    OrderStatus OrderStatus,
    string Reason,
    DateTime VoidedAtUtc);
