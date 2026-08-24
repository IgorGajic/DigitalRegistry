using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Exceptions;
using DigitalRegistry.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Commands.VoidOpenOrder;

internal sealed class VoidOpenOrderCommandHandler(
    IDigitalRegistryDbContext context,
    IInventoryAllocator inventoryAllocator,
    ICurrentUserService currentUser,
    IDateTimeService dateTime)
    : IRequestHandler<VoidOpenOrderCommand, Result<VoidResultDto>>
{
    public async Task<Result<VoidResultDto>> Handle(
        VoidOpenOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<VoidResultDto>.Unauthorized("The member of staff voiding this could not be identified.");
        }

        var order = await context.Orders
            .Include(candidate => candidate.OrderItems)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result<VoidResultDto>.NotFound($"Order {request.OrderId} was not found.");
        }

        if (order.Status == OrderStatus.Paid)
        {
            return Result<VoidResultDto>.Conflict(
                "This bill has been settled. Reverse it instead, which a manager has to authorise.");
        }

        // Everything the tab consumed goes back, summed per menu item so a drink ordered twice on
        // separate lines returns as one movement rather than two.
        var toReturn = order.OrderItems
            .GroupBy(item => item.MenuItemId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        Money total;

        try
        {
            total = order.VoidOpen();
        }
        catch (DomainException exception)
        {
            return Result<VoidResultDto>.Conflict(exception.Message);
        }

        if (toReturn.Count > 0)
        {
            var returned = await inventoryAllocator.ReturnAsync(toReturn, order.Id, cancellationToken);
            await inventoryAllocator.RefreshMenuAvailabilityAsync(returned, cancellationToken);
        }

        var record = new VoidRecord
        {
            RestaurantId = order.RestaurantId,
            OrderId = order.Id,
            Type = VoidType.OpenOrder,
            // The lines carry the detail; a whole-order void has no single item or quantity.
            Quantity = 0,
            Amount = total.Amount,
            Reason = request.Reason.Trim(),
            PerformedByUserId = userId,
            VoidedAtUtc = dateTime.UtcNow
        };

        context.VoidRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        return Result<VoidResultDto>.Success(new VoidResultDto(
            VoidRecordId: record.Id,
            OrderId: order.Id,
            Type: VoidType.OpenOrder,
            ItemName: null,
            Quantity: 0,
            Amount: total.Amount,
            RemainingTotal: 0m,
            OrderStatus: order.Status,
            Reason: record.Reason,
            VoidedAtUtc: record.VoidedAtUtc));
    }
}
