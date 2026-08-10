using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Tables.Queries.GetAvailableTables;

public class GetAvailableTablesQueryHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTimeService)
    : IRequestHandler<GetAvailableTablesQuery, Result<IReadOnlyList<TableAvailabilityDto>>>
{
    public async Task<Result<IReadOnlyList<TableAvailabilityDto>>> Handle(
        GetAvailableTablesQuery request,
        CancellationToken cancellationToken)
    {
        var now = dateTimeService.UtcNow;
        // Only meaningful to call a table "occupied" when the requested period includes the present.
        var periodIncludesNow = request.From <= now && now < request.To;

        var candidates = await context.Tables
            .AsNoTracking()
            .Where(table => table.IsActive && table.Capacity >= request.PartySize)
            .Select(table => new
            {
                table.Id,
                table.TableNumber,
                table.Capacity,

                // Mirrors ShiftTimeRange.Overlaps. It has to be restated as an inline predicate
                // because EF Core must translate the comparison into SQL and cannot call into the
                // value object to do it.
                IsReserved = table.Reservations.Any(reservation =>
                    (reservation.Status == ReservationStatus.Pending
                     || reservation.Status == ReservationStatus.Confirmed)
                    && reservation.StartTime < request.To
                    && request.From < reservation.EndTime),

                HasOpenOrder = table.Orders.Any(order =>
                    order.Status == OrderStatus.Open
                    || order.Status == OrderStatus.InPreparation
                    || order.Status == OrderStatus.Served)
            })
            .OrderBy(table => table.TableNumber)
            .ToListAsync(cancellationToken);

        var availability = candidates
            .Select(table => new TableAvailabilityDto(
                table.Id,
                table.TableNumber,
                table.Capacity,
                DetermineStatus(table.IsReserved, table.HasOpenOrder && periodIncludesNow)))
            .Where(table => request.IncludeUnavailable || table.Status == TableStatus.Available)
            .ToList();

        return Result<IReadOnlyList<TableAvailabilityDto>>.Success(availability);
    }

    /// <summary>
    /// An occupied table is reported as occupied even if it is also reserved, because that is the
    /// condition a member of staff has to deal with first.
    /// </summary>
    private static TableStatus DetermineStatus(bool isReserved, bool isOccupied) => isOccupied
        ? TableStatus.Occupied
        : isReserved
            ? TableStatus.Reserved
            : TableStatus.Available;
}
