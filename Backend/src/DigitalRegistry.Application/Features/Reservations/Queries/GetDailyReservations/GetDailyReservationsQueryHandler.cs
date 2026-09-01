using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reservations.Queries.GetDailyReservations;

public class GetDailyReservationsQueryHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTimeService)
    : IRequestHandler<GetDailyReservationsQuery, Result<IReadOnlyList<ReservationScheduleEntryDto>>>
{
    public async Task<Result<IReadOnlyList<ReservationScheduleEntryDto>>> Handle(
        GetDailyReservationsQuery request,
        CancellationToken cancellationToken)
    {
        var date = request.Date ?? dateTimeService.TodayUtc;
        var dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        // Any booking that overlaps the day appears on that day's sheet, including one that started
        // the previous evening and runs past midnight.
        var query = context.Reservations
            .AsNoTracking()
            .Where(reservation => reservation.StartTime < dayEnd && dayStart < reservation.EndTime);

        if (request.TableId is { } tableId)
        {
            query = query.Where(reservation => reservation.TableId == tableId);
        }

        var schedule = await query
            .OrderBy(reservation => reservation.StartTime)
            .ThenBy(reservation => reservation.Table!.TableNumber)
            .Select(reservation => new ReservationScheduleEntryDto(
                reservation.Id,
                reservation.TableId,
                reservation.Table!.TableNumber,
                reservation.StartTime,
                reservation.EndTime,
                reservation.PartySize,
                reservation.Status,
                reservation.GuestId,
                // A booking has exactly one of the two, so this is a choice rather than a fallback:
                // an account holder's name, or the name the desk wrote down for a telephone booking.
                reservation.Guest == null
                    ? reservation.ContactName!
                    : reservation.Guest.FirstName + " " + reservation.Guest.LastName,
                reservation.ContactPhone,
                reservation.TakenBy == null
                    ? null
                    : reservation.TakenBy.FirstName + " " + reservation.TakenBy.LastName))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ReservationScheduleEntryDto>>.Success(schedule);
    }
}
