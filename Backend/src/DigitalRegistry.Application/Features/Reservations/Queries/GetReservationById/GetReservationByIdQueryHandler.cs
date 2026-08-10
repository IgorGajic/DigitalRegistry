using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reservations.Queries.GetReservationById;

public class GetReservationByIdQueryHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetReservationByIdQuery, Result<ReservationDto>>
{
    public async Task<Result<ReservationDto>> Handle(
        GetReservationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var reservation = await context.Reservations
            .AsNoTracking()
            .Where(candidate => candidate.Id == request.Id)
            .Select(candidate => new
            {
                Dto = new ReservationDto(
                    candidate.Id,
                    candidate.TableId,
                    candidate.Table!.TableNumber,
                    candidate.StartTime,
                    candidate.EndTime,
                    candidate.PartySize,
                    candidate.Status),
                candidate.GuestId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (reservation is null)
        {
            return Result<ReservationDto>.NotFound($"Reservation {request.Id} was not found.");
        }

        var isStaff = currentUserService.IsInAnyRole(UserRole.Waiter, UserRole.Manager, UserRole.Owner);

        if (!isStaff && reservation.GuestId != currentUserService.UserId)
        {
            // A 404 rather than a 403: a guest should not be able to confirm that a booking id they
            // guessed belongs to somebody else.
            return Result<ReservationDto>.NotFound($"Reservation {request.Id} was not found.");
        }

        return Result<ReservationDto>.Success(reservation.Dto);
    }
}
