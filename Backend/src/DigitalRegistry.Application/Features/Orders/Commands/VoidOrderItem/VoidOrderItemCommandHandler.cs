using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Commands.VoidOrderItem;

internal sealed class VoidOrderItemCommandHandler(
    IDigitalRegistryDbContext context,
    IInventoryAllocator inventoryAllocator,
    ICurrentUserService currentUser,
    IDateTimeService dateTime)
    : IRequestHandler<VoidOrderItemCommand, Result<VoidResultDto>>
{
    public async Task<Result<VoidResultDto>> Handle(
        VoidOrderItemCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<VoidResultDto>.Unauthorized("The member of staff voiding this could not be identified.");
        }

        var order = await context.Orders
            .Include(candidate => candidate.OrderItems)
            .ThenInclude(item => item.MenuItem)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result<VoidResultDto>.NotFound($"Order {request.OrderId} was not found.");
        }

        if (!order.IsEditable)
        {
            return Result<VoidResultDto>.Conflict(
                $"This order is {order.Status}; its lines can no longer be voided. "
                + "A settled bill is reversed instead.");
        }

        var line = order.OrderItems.FirstOrDefault(item => item.Id == request.OrderItemId);

        if (line is null)
        {
            return Result<VoidResultDto>.NotFound($"Line {request.OrderItemId} is not on this order.");
        }

        var quantity = request.Quantity ?? line.Quantity;

        if (quantity > line.Quantity)
        {
            return Result<VoidResultDto>.Invalid(
                $"Only {line.Quantity} of this line remain; {quantity} cannot be cancelled.");
        }

        // Captured before the void, because cancelling the whole line detaches it from the order.
        var menuItemId = line.MenuItemId;
        var itemName = line.MenuItem?.Name;
        var removesLine = quantity == line.Quantity;

        var amount = order.VoidItem(line, quantity);

        if (removesLine)
        {
            // Detaching the line from the collection is not enough on its own: the row has to go too.
            context.OrderItems.Remove(line);
        }

        var returned = await inventoryAllocator.ReturnAsync(
            new Dictionary<Guid, int> { [menuItemId] = quantity },
            order.Id,
            cancellationToken);

        await inventoryAllocator.RefreshMenuAvailabilityAsync(returned, cancellationToken);

        var record = new VoidRecord
        {
            RestaurantId = order.RestaurantId,
            OrderId = order.Id,
            Type = VoidType.Item,
            MenuItemId = menuItemId,
            ItemName = itemName,
            Quantity = quantity,
            Amount = amount.Amount,
            Reason = request.Reason.Trim(),
            PerformedByUserId = userId,
            VoidedAtUtc = dateTime.UtcNow
        };

        context.VoidRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        return Result<VoidResultDto>.Success(new VoidResultDto(
            VoidRecordId: record.Id,
            OrderId: order.Id,
            Type: VoidType.Item,
            ItemName: itemName,
            Quantity: quantity,
            Amount: amount.Amount,
            RemainingTotal: order.Total.Amount,
            OrderStatus: order.Status,
            Reason: record.Reason,
            VoidedAtUtc: record.VoidedAtUtc));
    }
}
