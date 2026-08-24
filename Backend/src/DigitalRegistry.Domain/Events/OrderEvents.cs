using DigitalRegistry.Domain.Common;

namespace DigitalRegistry.Domain.Events;

/// <summary>Raised when any order is opened. Drives the kitchen/bar display.</summary>
public sealed record OrderCreatedDomainEvent(Guid OrderId, Guid TableId, int TableNumber, Guid? WaiterId)
    : DomainEventBase;

/// <summary>
/// Raised in addition to <see cref="OrderCreatedDomainEvent"/> when the order originated from a
/// guest scanning a table QR code, so floor staff can be alerted that nobody took the order.
/// </summary>
public sealed record GuestQrOrderPlacedDomainEvent(Guid OrderId, Guid TableId, int TableNumber)
    : DomainEventBase;

/// <summary>Raised when a line on an open order is added, changed or removed.</summary>
public sealed record OrderItemUpdatedDomainEvent(
    Guid OrderId,
    Guid OrderItemId,
    Guid MenuItemId,
    int Quantity,
    bool Removed) : DomainEventBase;

/// <summary>Raised once payment has been recorded and the order is closed.</summary>
public sealed record OrderPaidDomainEvent(Guid OrderId, Guid TableId, decimal Amount) : DomainEventBase;

/// <summary>
/// Raised when a whole order is cancelled or reversed, so the floor screen frees the table.
/// </summary>
/// <param name="Amount">What came off the bill.</param>
/// <param name="WasPaid">
/// True when the bill had already been settled and a counter-transaction was written. False when the
/// tab was cancelled before payment, which never touched the takings — the two mean different things
/// to anyone reconciling the till.
/// </param>
public sealed record OrderVoidedDomainEvent(
    Guid OrderId,
    Guid TableId,
    decimal Amount,
    bool WasPaid) : DomainEventBase;
