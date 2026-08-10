using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Commands.ProcessPayment;

internal sealed class ProcessPaymentCommandHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<ProcessPaymentCommand, Result<TransactionDto>>
{
    public async Task<Result<TransactionDto>> Handle(
        ProcessPaymentCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } waiterId)
        {
            return Result<TransactionDto>.Forbidden("Only signed-in staff can take payment.");
        }

        var order = await context.Orders
            .Include(candidate => candidate.OrderItems)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result<TransactionDto>.NotFound($"Order {request.OrderId} was not found.");
        }

        if (order.IsClosed)
        {
            return Result<TransactionDto>.Conflict($"This order is already {order.Status}.");
        }

        if (order.OrderItems.Count == 0)
        {
            return Result<TransactionDto>.Conflict("An empty order cannot be paid.");
        }

        // Checked ahead of the unique index on Transaction.OrderId so a double submission comes back
        // as a clear conflict rather than a constraint violation.
        var alreadyPaid = await context.Transactions
            .AnyAsync(transaction => transaction.OrderId == order.Id, cancellationToken);

        if (alreadyPaid)
        {
            return Result<TransactionDto>.Conflict("A payment has already been recorded for this order.");
        }

        // The entity totals its own lines, sets the status and raises OrderPaidDomainEvent.
        var transaction = order.Pay(waiterId, request.PaymentMethod);

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);

        return Result<TransactionDto>.Success(new TransactionDto(
            transaction.Id,
            transaction.OrderId,
            transaction.ProcessedByWaiterId,
            transaction.Amount,
            transaction.PaymentMethod,
            transaction.TransactionDate));
    }
}
