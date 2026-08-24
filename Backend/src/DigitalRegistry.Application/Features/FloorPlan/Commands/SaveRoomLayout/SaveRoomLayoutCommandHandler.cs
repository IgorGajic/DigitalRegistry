using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.SaveRoomLayout;

public class SaveRoomLayoutCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<SaveRoomLayoutCommand, Result<RoomDto>>
{
    public async Task<Result<RoomDto>> Handle(
        SaveRoomLayoutCommand request,
        CancellationToken cancellationToken)
    {
        var room = await context.Rooms
            .FirstOrDefaultAsync(candidate => candidate.Id == request.RoomId, cancellationToken);

        if (room is null)
        {
            return Result<RoomDto>.NotFound("No such room.");
        }

        var requestedIds = request.Tables.Select(table => table.TableId).ToList();

        // Loaded through the tenant filter, so a layout naming another restaurant's table simply
        // finds nothing and is reported as unknown rather than moving it.
        var tables = await context.Tables
            .Where(table => requestedIds.Contains(table.Id) || table.RoomId == room.Id)
            .ToListAsync(cancellationToken);

        var known = tables.ToDictionary(table => table.Id);

        var unknown = requestedIds.Where(id => !known.ContainsKey(id)).ToList();

        if (unknown.Count > 0)
        {
            return Result<RoomDto>.NotFound(
                $"The layout refers to {unknown.Count} table(s) that do not belong to this restaurant.");
        }

        foreach (var layout in request.Tables)
        {
            var table = known[layout.TableId];

            if (layout.PositionX + layout.Width > room.CanvasWidth
                || layout.PositionY + layout.Height > room.CanvasHeight)
            {
                return Result<RoomDto>.Invalid(
                    $"Table {table.TableNumber} does not fit inside the room's {room.CanvasWidth}"
                    + $"×{room.CanvasHeight} area.");
            }

            table.RoomId = room.Id;
            table.PositionX = layout.PositionX;
            table.PositionY = layout.PositionY;
            table.Width = layout.Width;
            table.Height = layout.Height;
            table.Shape = layout.Shape;
            table.Rotation = layout.Rotation;
        }

        // Anything still assigned to this room but missing from the layout was dragged out of it.
        foreach (var removed in tables.Where(table =>
                     table.RoomId == room.Id && !requestedIds.Contains(table.Id)))
        {
            removed.RoomId = null;
        }

        await context.SaveChangesAsync(cancellationToken);

        var saved = tables
            .Where(table => table.RoomId == room.Id)
            .OrderBy(table => table.TableNumber)
            .Select(table => new FloorPlanTableDto(
                Id: table.Id,
                TableNumber: table.TableNumber,
                Capacity: table.Capacity,
                // The editor is not a live view; occupancy is whatever the floor screen reports next.
                Status: Domain.Enums.TableStatus.Available,
                Shape: table.Shape,
                PositionX: table.PositionX,
                PositionY: table.PositionY,
                Width: table.Width,
                Height: table.Height,
                Rotation: table.Rotation,
                IsActive: table.IsActive,
                OpenOrderIds: [],
                OpenOrderTotal: 0m,
                OldestOpenOrderAtUtc: null))
            .ToList();

        return Result<RoomDto>.Success(new RoomDto(
            room.Id,
            room.Name,
            room.DisplayOrder,
            room.CanvasWidth,
            room.CanvasHeight,
            saved));
    }
}
