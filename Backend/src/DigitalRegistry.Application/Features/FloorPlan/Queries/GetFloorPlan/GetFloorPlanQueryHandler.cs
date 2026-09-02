using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Features.Tables;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.FloorPlan.Queries.GetFloorPlan;

public class GetFloorPlanQueryHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTimeService)
    : IRequestHandler<GetFloorPlanQuery, Result<FloorPlanDto>>
{
    public async Task<Result<FloorPlanDto>> Handle(
        GetFloorPlanQuery request,
        CancellationToken cancellationToken)
    {
        var now = dateTimeService.UtcNow;

        // One query for every table in the restaurant, rooms included, rather than one per room:
        // a floor plan is small and the screen needs all of it at once anyway.
        var tables = await context.Tables
            .AsNoTracking()
            .Where(table => request.IncludeInactive || table.IsActive)
            .Select(table => new
            {
                table.Id,
                table.RoomId,
                table.TableNumber,
                table.Capacity,
                table.IsActive,
                table.Shape,
                table.PositionX,
                table.PositionY,
                table.Width,
                table.Height,
                table.Rotation,

                // A reservation counts against the table only while it is actually running; one
                // booked for this evening must not grey out the table all afternoon.
                IsReserved = table.Reservations.AsQueryable()
                    .Where(TableStatusRules.HoldsTable)
                    .Any(reservation => reservation.StartTime <= now && now < reservation.EndTime),

                OpenOrderIds = table.Orders.AsQueryable()
                    .Where(TableStatusRules.IsOpenTab)
                    .OrderBy(order => order.CreatedAt)
                    .Select(order => order.Id)
                    .ToList(),

                OpenOrderTotal = table.Orders.AsQueryable()
                    .Where(TableStatusRules.IsOpenTab)
                    .SelectMany(order => order.OrderItems)
                    .Sum(item => (decimal?)(item.UnitPrice * item.Quantity)) ?? 0m,

                OldestOpenOrderAtUtc = table.Orders.AsQueryable()
                    .Where(TableStatusRules.IsOpenTab)
                    .Min(order => (DateTime?)order.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        var rooms = await context.Rooms
            .AsNoTracking()
            .OrderBy(room => room.DisplayOrder)
            .ThenBy(room => room.Name)
            .Select(room => new { room.Id, room.Name, room.DisplayOrder, room.CanvasWidth, room.CanvasHeight })
            .ToListAsync(cancellationToken);

        // Landmarks, in one query like the tables. Unfiltered by `IncludeInactive`: a fixture has no
        // state to be in, so there is nothing for that flag to mean here.
        var fixtures = await context.RoomFixtures
            .AsNoTracking()
            .OrderBy(fixture => fixture.DisplayOrder)
            .Select(fixture => new
            {
                fixture.RoomId,
                Dto = new RoomFixtureDto(
                    Id: fixture.Id,
                    Kind: fixture.Kind,
                    Label: fixture.Label,
                    Shape: fixture.Shape,
                    Tone: fixture.Tone,
                    PositionX: fixture.PositionX,
                    PositionY: fixture.PositionY,
                    Width: fixture.Width,
                    Height: fixture.Height,
                    Rotation: fixture.Rotation,
                    DisplayOrder: fixture.DisplayOrder)
            })
            .ToListAsync(cancellationToken);

        var fixturesByRoom = fixtures.ToLookup(entry => entry.RoomId, entry => entry.Dto);

        var byRoom = tables
            .Select(table => new
            {
                table.RoomId,
                Dto = new FloorPlanTableDto(
                    Id: table.Id,
                    TableNumber: table.TableNumber,
                    Capacity: table.Capacity,
                    Status: TableStatusRules.Determine(
                        isActive: table.IsActive,
                        isOccupied: table.OpenOrderIds.Count > 0,
                        isReserved: table.IsReserved),
                    Shape: table.Shape,
                    PositionX: table.PositionX,
                    PositionY: table.PositionY,
                    Width: table.Width,
                    Height: table.Height,
                    Rotation: table.Rotation,
                    IsActive: table.IsActive,
                    OpenOrderIds: table.OpenOrderIds,
                    OpenOrderTotal: decimal.Round(table.OpenOrderTotal, 2),
                    OldestOpenOrderAtUtc: table.OldestOpenOrderAtUtc)
            })
            .ToLookup(entry => entry.RoomId, entry => entry.Dto);

        var roomDtos = rooms
            .Select(room => new RoomDto(
                Id: room.Id,
                Name: room.Name,
                DisplayOrder: room.DisplayOrder,
                CanvasWidth: room.CanvasWidth,
                CanvasHeight: room.CanvasHeight,
                Tables: byRoom[room.Id].OrderBy(table => table.TableNumber).ToList(),
                Fixtures: fixturesByRoom[room.Id].ToList()))
            .ToList();

        var unplaced = byRoom[null].OrderBy(table => table.TableNumber).ToList();

        return Result<FloorPlanDto>.Success(new FloorPlanDto(roomDtos, unplaced));
    }
}
