using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Tables.Commands.UpdateTable;

public class UpdateTableCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<UpdateTableCommand, Result>
{
    public async Task<Result> Handle(UpdateTableCommand request, CancellationToken cancellationToken)
    {
        var table = await context.Tables
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (table is null)
        {
            return Result.NotFound($"Table {request.Id} was not found.");
        }

        var numberTaken = await context.Tables
            .AnyAsync(
                candidate => candidate.TableNumber == request.TableNumber && candidate.Id != request.Id,
                cancellationToken);

        if (numberTaken)
        {
            return Result.Conflict($"Table number {request.TableNumber} is already in use.");
        }

        // Reducing capacity below an existing booking's party size would leave that party without a
        // table they can actually sit at, so refuse rather than silently invalidate the reservation.
        if (request.Capacity < table.Capacity)
        {
            var largestBookedParty = await context.Reservations
                .Where(reservation => reservation.TableId == table.Id
                                      && reservation.EndTime > DateTime.UtcNow
                                      && (reservation.Status == ReservationStatus.Pending
                                          || reservation.Status == ReservationStatus.Confirmed))
                .Select(reservation => (int?)reservation.PartySize)
                .MaxAsync(cancellationToken);

            if (largestBookedParty > request.Capacity)
            {
                return Result.Conflict(
                    $"An upcoming reservation is for {largestBookedParty} guests; capacity cannot be "
                    + $"reduced to {request.Capacity}.");
            }
        }

        table.TableNumber = request.TableNumber;
        table.Capacity = request.Capacity;
        table.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
