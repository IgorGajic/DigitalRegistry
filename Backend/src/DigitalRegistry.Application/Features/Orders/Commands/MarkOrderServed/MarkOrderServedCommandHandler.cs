using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Commands.MarkOrderServed;

public class MarkOrderServedCommandHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<MarkOrderServedCommand, Result>
{
    public async Task<Result> Handle(
        MarkOrderServedCommand request,
        CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .FirstOrDefaultAsync(candidate => candidate.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result.NotFound("No such order.");
        }

        try
        {
            // Open or InPreparation only; the entity refuses anything else. Serving a paid or voided
            // order is not a mistake worth a special message, it is a stale screen — two waiters with
            // the floor open, one of whom pressed the button a moment earlier.
            // Who, not just when. A guest QR round has no waiter on it at all, so this is the only
            // record that the round was somebody's work — and the owner's per-waiter report has
            // nothing to measure service by without it.
            order.MarkServed(dateTime.UtcNow, currentUser.UserId);
        }
        catch (DomainException exception)
        {
            return Result.Conflict(exception.Message);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
