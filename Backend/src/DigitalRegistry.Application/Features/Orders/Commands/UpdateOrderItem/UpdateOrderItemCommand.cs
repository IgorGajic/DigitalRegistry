using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.UpdateOrderItem;

/// <summary>What kind of change to make to an order's lines.</summary>
public enum OrderItemChange
{
    /// <summary>Put a new line on the order. Needs <c>MenuItemId</c> and <c>Quantity</c>.</summary>
    Add = 1,

    /// <summary>Change an existing line's quantity. Needs <c>OrderItemId</c> and <c>Quantity</c>.</summary>
    ChangeQuantity = 2,

    /// <summary>Change an existing line's kitchen notes. Needs <c>OrderItemId</c>.</summary>
    ChangeNotes = 3,

    /// <summary>Take a line off the order. Needs <c>OrderItemId</c>.</summary>
    Remove = 4
}

/// <summary>
/// Adds, changes or removes a line on an order that is still open. Waiter and owner only.
/// </summary>
/// <remarks>
/// Stock moves with the change: adding or increasing a quantity deducts, reducing or removing
/// returns, and only the difference is ever moved.
/// </remarks>
public record UpdateOrderItemCommand(
    Guid OrderId,
    OrderItemChange Change,
    Guid? OrderItemId = null,
    Guid? MenuItemId = null,
    int? Quantity = null,
    string? Notes = null) : IRequest<Result<OrderDto>>;
