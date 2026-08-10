using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reservations.Queries.GetGuestReservations;

public class GetGuestReservationsQueryHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUserService,
    IDateTimeService dateTimeService)
    : IRequestHandler<GetGuestReservationsQuery, Result<IReadOnlyList<ReservationDto>>>
{
    public async Task<Result<IReadOnlyList<ReservationDto>>> Handle(
        GetGuestReservationsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } guestId)
        {
            return Result<IReadOnlyList<ReservationDto>>.Forbidden(
                "A table QR session has no reservations. Sign in with an account first.");
        }

        // Filtered by the caller's own id from their token, so this endpoint cannot return anybody
        // else's bookings regardless of what the client sends.
        var query = context.Reservations
            .AsNoTracking()
            .Where(reservation => reservation.GuestId == guestId);

        if (!request.IncludePast)
        {
            query = query.Where(reservation => reservation.EndTime >= dateTimeService.UtcNow);
        }

        var reservations = await query
            .OrderBy(reservation => reservation.StartTime)
            .Select(reservation => new ReservationDto(
                reservation.Id,
                reservation.TableId,
                reservation.Table!.TableNumber,
                reservation.StartTime,
                reservation.EndTime,
                reservation.PartySize,
                reservation.Status))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ReservationDto>>.Success(reservations);
    }
}
