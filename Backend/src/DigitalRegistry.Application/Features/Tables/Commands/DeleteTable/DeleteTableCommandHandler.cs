using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Tables.Commands.DeleteTable;

public class DeleteTableCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<DeleteTableCommand, Result>
{
    public async Task<Result> Handle(DeleteTableCommand request, CancellationToken cancellationToken)
    {
        var table = await context.Tables
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (table is null)
        {
            return Result.NotFound($"Table {request.Id} was not found.");
        }

        var hasOrders = await context.Orders.AnyAsync(order => order.TableId == table.Id, cancellationToken);
        var hasReservations = await context.Reservations
            .AnyAsync(reservation => reservation.TableId == table.Id, cancellationToken);

        if (hasOrders || hasReservations)
        {
            // The foreign keys are Restrict, so attempting the delete would fail at the database.
            // Say so plainly and point at the supported alternative.
            return Result.Conflict(
                $"Table {table.TableNumber} has order or reservation history and cannot be deleted. "
                + "Deactivate it instead.");
        }

        context.Tables.Remove(table);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
