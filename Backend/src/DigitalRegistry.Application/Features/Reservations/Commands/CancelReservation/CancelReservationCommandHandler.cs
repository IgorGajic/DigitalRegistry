using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reservations.Commands.CancelReservation;

public class CancelReservationCommandHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<CancelReservationCommand, Result>
{
    public async Task<Result> Handle(CancelReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await context.Reservations
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (reservation is null)
        {
            return Result.NotFound($"Reservation {request.Id} was not found.");
        }

        // Managers and owners may cancel anything; a guest is confined to their own bookings.
        var canCancelAnyReservation = currentUserService.IsInAnyRole(UserRole.Manager, UserRole.Owner);

        if (!canCancelAnyReservation && reservation.GuestId != currentUserService.UserId)
        {
            // Deliberately the same shape as a genuine forbidden response rather than a 404, since
            // the caller already had to know the id to get here.
            return Result.Forbidden("You can only cancel your own reservations.");
        }

        if (reservation.Status is ReservationStatus.Cancelled)
        {
            return Result.Success();
        }

        if (reservation.Status is ReservationStatus.Completed)
        {
            return Result.Conflict("This reservation has already been completed and cannot be cancelled.");
        }

        // Raises ReservationCancelledDomainEvent, dispatched once the save commits.
        reservation.Cancel();
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
