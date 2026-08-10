using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reservations.Commands.CheckInReservation;

public class CheckInReservationCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<CheckInReservationCommand, Result>
{
    public async Task<Result> Handle(CheckInReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await context.Reservations
            .Include(candidate => candidate.Table)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (reservation is null)
        {
            return Result.NotFound($"Reservation {request.Id} was not found.");
        }

        if (reservation.Table is null)
        {
            return Result.NotFound($"The table for reservation {request.Id} no longer exists.");
        }

        if (!reservation.BlocksTable)
        {
            return Result.Conflict($"A {reservation.Status} reservation cannot be checked in.");
        }

        // Raises ReservationArrivalAlertDomainEvent; the table is passed in so the alert can name the
        // table number.
        reservation.MarkArrived(reservation.Table);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
