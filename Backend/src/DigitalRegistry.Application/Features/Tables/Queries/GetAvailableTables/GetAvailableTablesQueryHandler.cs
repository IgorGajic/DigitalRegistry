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

                // The overlap test mirrors ShiftTimeRange.Overlaps. It has to be restated as an inline
                // predicate because EF Core must translate the comparison into SQL and cannot call
                // into the value object to do it.
                IsReserved = table.Reservations.AsQueryable()
                    .Where(TableStatusRules.HoldsTable)
                    .Any(reservation =>
                        reservation.StartTime < request.To && request.From < reservation.EndTime),

                HasOpenOrder = table.Orders.AsQueryable().Any(TableStatusRules.IsOpenTab)
            })
            .OrderBy(table => table.TableNumber)
            .ToListAsync(cancellationToken);

        var availability = candidates
            .Select(table => new TableAvailabilityDto(
                table.Id,
                table.TableNumber,
                table.Capacity,
                TableStatusRules.Determine(
                    isActive: true,
                    isOccupied: table.HasOpenOrder && periodIncludesNow,
                    isReserved: table.IsReserved)))
            .Where(table => request.IncludeUnavailable || table.Status == TableStatus.Available)
            .ToList();

        return Result<IReadOnlyList<TableAvailabilityDto>>.Success(availability);
    }
}
