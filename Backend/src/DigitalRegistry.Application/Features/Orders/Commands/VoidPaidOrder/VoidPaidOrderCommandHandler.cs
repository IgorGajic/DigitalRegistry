using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Commands.VoidPaidOrder;

internal sealed class VoidPaidOrderCommandHandler(
    IDigitalRegistryDbContext context,
    IInventoryAllocator inventoryAllocator,
    ICurrentUserService currentUser,
    IDateTimeService dateTime)
    : IRequestHandler<VoidPaidOrderCommand, Result<VoidResultDto>>
{
    public async Task<Result<VoidResultDto>> Handle(
        VoidPaidOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } approverId)
        {
            return Result<VoidResultDto>.Unauthorized("The authorising manager could not be identified.");
        }

        var order = await context.Orders
            .Include(candidate => candidate.OrderItems)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result<VoidResultDto>.NotFound($"Order {request.OrderId} was not found.");
        }

        if (order.Status != OrderStatus.Paid)
        {
            return Result<VoidResultDto>.Conflict(
                $"Only a settled bill can be reversed; this one is {order.Status}.");
        }

        // The payment, not a previous reversal — the original is the row this backs out.
        var payment = await context.Transactions
            .FirstOrDefaultAsync(
                candidate => candidate.OrderId == order.Id && candidate.ReversesTransactionId == null,
                cancellationToken);

        if (payment is null)
        {
            return Result<VoidResultDto>.Conflict(
                "This order is marked paid but carries no payment record; it cannot be reversed automatically.");
        }

        var toReturn = order.OrderItems
            .GroupBy(item => item.MenuItemId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));

        Transaction reversal;

        try
        {
            reversal = order.Reverse(payment, approverId);
        }
        catch (DomainException exception)
        {
            return Result<VoidResultDto>.Conflict(exception.Message);
        }

        context.Transactions.Add(reversal);

        if (toReturn.Count > 0)
        {
            var returned = await inventoryAllocator.ReturnAsync(toReturn, order.Id, cancellationToken);
            await inventoryAllocator.RefreshMenuAvailabilityAsync(returned, cancellationToken);
        }

        var record = new VoidRecord
        {
            RestaurantId = order.RestaurantId,
            OrderId = order.Id,
            Type = VoidType.PaidOrder,
            Quantity = 0,
            Amount = payment.Amount,
            Reason = request.Reason.Trim(),
            // The manager both carries this out and authorises it. Recorded in both fields so the
            // report reads consistently against item voids, where the two are different people.
            PerformedByUserId = approverId,
            ApprovedByUserId = approverId,
            VoidedAtUtc = dateTime.UtcNow
        };

        context.VoidRecords.Add(record);
        await context.SaveChangesAsync(cancellationToken);

        return Result<VoidResultDto>.Success(new VoidResultDto(
            VoidRecordId: record.Id,
            OrderId: order.Id,
            Type: VoidType.PaidOrder,
            ItemName: null,
            Quantity: 0,
            Amount: payment.Amount,
            RemainingTotal: 0m,
            OrderStatus: order.Status,
            Reason: record.Reason,
            VoidedAtUtc: record.VoidedAtUtc));
    }
}
