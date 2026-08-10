using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reservations.Commands.CreateReservation;

public class CreateReservationCommandHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateReservationCommand, Result<ReservationDto>>
{
    public async Task<Result<ReservationDto>> Handle(
        CreateReservationCommand request,
        CancellationToken cancellationToken)
    {
        // An anonymous QR table session carries the guest role but no user id. Such a guest is
        // already seated, so there is nobody to attribute a booking to.
        if (currentUserService.UserId is not { } guestId)
        {
            return Result<ReservationDto>.Forbidden(
                "A table QR session cannot make reservations. Sign in with an account first.");
        }

        var table = await context.Tables
            .FirstOrDefaultAsync(candidate => candidate.Id == request.TableId, cancellationToken);

        if (table is null)
        {
            return Result<ReservationDto>.NotFound($"Table {request.TableId} was not found.");
        }

        if (!table.IsActive)
        {
            return Result<ReservationDto>.Conflict($"Table {table.TableNumber} is not in service.");
        }

        if (!table.CanSeat(request.PartySize))
        {
            return Result<ReservationDto>.Conflict(
                $"Table {table.TableNumber} seats {table.Capacity}; it cannot take a party of "
                + $"{request.PartySize}.");
        }

        // Mirrors ShiftTimeRange.Overlaps, restated inline because EF Core has to translate it to SQL.
        var alreadyBooked = await context.Reservations.AnyAsync(
            existing => existing.TableId == table.Id
                        && (existing.Status == ReservationStatus.Pending
                            || existing.Status == ReservationStatus.Confirmed)
                        && existing.StartTime < request.EndTime
                        && request.StartTime < existing.EndTime,
            cancellationToken);

        if (alreadyBooked)
        {
            return Result<ReservationDto>.Conflict(
                $"Table {table.TableNumber} is already booked during that period.");
        }

        var reservation = new Reservation
        {
            GuestId = guestId,
            TableId = table.Id,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            PartySize = request.PartySize,
            Status = ReservationStatus.Pending
        };

        context.Reservations.Add(reservation);
        await context.SaveChangesAsync(cancellationToken);

        return Result<ReservationDto>.Success(new ReservationDto(
            reservation.Id,
            table.Id,
            table.TableNumber,
            reservation.StartTime,
            reservation.EndTime,
            reservation.PartySize,
            reservation.Status));
    }
}
