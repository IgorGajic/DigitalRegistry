using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Commands.MarkOrderServed;

public class MarkOrderServedCommandHandler(IDigitalRegistryDbContext context)
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
            order.MarkServed();
        }
        catch (DomainException exception)
        {
            return Result.Conflict(exception.Message);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
