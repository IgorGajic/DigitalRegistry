using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.UpdateOrderItem;

/// <summary>What kind of change to make to an order's lines.</summary>
/// <remarks>
/// Every member here only ever adds to what a guest owes. Taking something off a tab is a void — see
/// <c>VoidOrderItemCommand</c> — because it needs a reason and an audit record.
/// </remarks>
public enum OrderItemChange
{
    /// <summary>Put a new line on the order. Needs <c>MenuItemId</c> and <c>Quantity</c>.</summary>
    Add = 1,

    /// <summary>
    /// Increase an existing line's quantity. Needs <c>OrderItemId</c> and <c>Quantity</c>.
    /// </summary>
    /// <remarks>
    /// Only upwards. A quantity below the current one is rejected rather than treated as a partial
    /// removal, which would be a way around the void report.
    /// </remarks>
    IncreaseQuantity = 2,

    /// <summary>Change an existing line's kitchen notes. Needs <c>OrderItemId</c>.</summary>
    ChangeNotes = 3
}

/// <summary>
/// Adds to a tab that is still open. Waiter and owner only.
/// </summary>
/// <remarks>
/// Stock moves with the change, and only the difference is ever moved.
/// <para>
/// This command cannot reduce a bill. Removing a line or cutting a quantity goes through the void
/// endpoints instead, so that anything coming off a guest's tab leaves a record of who did it and
/// why — which is what makes the void report a usable control rather than a partial one.
/// </para>
/// </remarks>
public record UpdateOrderItemCommand(
    Guid OrderId,
    OrderItemChange Change,
    Guid? OrderItemId = null,
    Guid? MenuItemId = null,
    int? Quantity = null,
    string? Notes = null) : IRequest<Result<OrderDto>>;
