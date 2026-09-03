using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Commands.ReopenOrderForService;

public class ReopenOrderForServiceCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<ReopenOrderForServiceCommand, Result>
{
    public async Task<Result> Handle(
        ReopenOrderForServiceCommand request,
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
            // Served only. A round that has since been paid for is not something to put back on a
            // queue — the guest has the drinks and the money is in the till.
            order.ReopenForService();
        }
        catch (DomainException exception)
        {
            return Result.Conflict(exception.Message);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
