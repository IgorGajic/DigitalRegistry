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
        var isStaff = currentUserService.IsInAnyRole(UserRole.Waiter, UserRole.Manager, UserRole.Owner);
        var contactName = string.IsNullOrWhiteSpace(request.ContactName)
            ? null
            : request.ContactName.Trim();

        // Only the desk books on somebody else's behalf. A guest sending a name would otherwise be
        // able to detach a booking from their own account and cancel rules along with it.
        if (contactName is not null && !isStaff)
        {
            return Result<ReservationDto>.Forbidden(
                "Only staff can book on a guest's behalf. Your booking is made in your own name.");
        }

        if (contactName is null)
        {
            // An anonymous QR table session carries the guest role but no user id. Such a guest is
            // already seated, so there is nobody to attribute a booking to.
            if (currentUserService.UserId is null)
            {
                return Result<ReservationDto>.Forbidden(
                    "A table QR session cannot make reservations. Sign in with an account first.");
            }

            // Staff who give no name would file the booking under themselves, which is the thing
            // this endpoint exists to stop.
            if (isStaff)
            {
                return Result<ReservationDto>.Invalid(
                    "A booking taken by staff must say who it is for.");
            }
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
            // Exactly one of the two is set: a guest's own booking carries their account, a desk
            // booking carries the name that was written down and no account at all.
            GuestId = contactName is null ? currentUserService.UserId : null,
            ContactName = contactName,
            ContactPhone = contactName is null || string.IsNullOrWhiteSpace(request.ContactPhone)
                ? null
                : request.ContactPhone.Trim(),
            TakenByUserId = contactName is null ? null : currentUserService.UserId,
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
