using DigitalRegistry.Domain.Entities;

namespace DigitalRegistry.Application.Features.Orders;

/// <summary>
/// Projects a loaded <see cref="Order"/> onto its DTO.
/// </summary>
/// <remarks>
/// Shared by the order handlers so the response shape is defined once. Expects
/// <see cref="Order.OrderItems"/>, each item's menu item, and the order's table to be loaded.
/// </remarks>
internal static class OrderMapping
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.TableId,
        order.Table?.TableNumber ?? 0,
        order.WaiterId,
        order.PlacedByGuest,
        order.Status,
        order.CreatedAt,
        order.Total.Amount,
        order.OrderItems
            .Select(item => new OrderItemDto(
                item.Id,
                item.MenuItemId,
                item.MenuItem?.Name ?? string.Empty,
                item.Quantity,
                item.UnitPrice,
                item.LineTotal.Amount,
                item.Notes))
            .ToList());
}
