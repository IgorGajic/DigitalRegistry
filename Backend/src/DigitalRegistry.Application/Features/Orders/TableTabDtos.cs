using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Orders;

/// <summary>One line of what a table has had, priced as it was ordered.</summary>
public record TableTabLineDto(
    string MenuItemName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? Notes);

/// <summary>
/// One round: everything sent to the bar at the same moment.
/// </summary>
/// <param name="PlacedByGuest">
/// True for a round the table sent through the QR code, false for one the waiter rang in. Shown so a
/// guest can tell their own orders from what the waiter added, rather than being surprised by lines
/// they never pressed a button for.
/// </param>
public record TableTabRoundDto(
    Guid OrderId,
    DateTime CreatedAtUtc,
    OrderStatus Status,
    bool PlacedByGuest,
    IReadOnlyList<TableTabLineDto> Lines);

/// <summary>
/// What a table has had so far, across every round still running.
/// </summary>
/// <remarks>
/// Not a bill: nothing here can be settled from the guest's phone, and payment still goes through
/// the till. It exists so a guest can see what they have already asked for before asking for more.
/// </remarks>
public record TableTabDto(
    Guid TableId,
    int TableNumber,
    int ItemCount,
    decimal Total,
    IReadOnlyList<TableTabRoundDto> Rounds);
