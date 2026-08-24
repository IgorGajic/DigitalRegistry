using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.UpdateRoom;

public class UpdateRoomCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<UpdateRoomCommand, Result<RoomDto>>
{
    public async Task<Result<RoomDto>> Handle(UpdateRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await context.Rooms
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (room is null)
        {
            return Result<RoomDto>.NotFound("No such room.");
        }

        var name = request.Name.Trim();

        var nameTaken = await context.Rooms
            .AnyAsync(other => other.Id != room.Id && other.Name == name, cancellationToken);

        if (nameTaken)
        {
            return Result<RoomDto>.Conflict($"A room called '{name}' already exists.");
        }

        // Shrinking must not strand a table beyond the edge of the editor, where it could not be
        // dragged back into view.
        var strandedTable = await context.Tables
            .Where(table => table.RoomId == room.Id)
            .Where(table => table.PositionX + table.Width > request.CanvasWidth
                            || table.PositionY + table.Height > request.CanvasHeight)
            .Select(table => (int?)table.TableNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (strandedTable is { } tableNumber)
        {
            return Result<RoomDto>.Conflict(
                $"Table {tableNumber} would fall outside a {request.CanvasWidth}×{request.CanvasHeight} "
                + "room. Move it first, or choose a larger area.");
        }

        room.Name = name;
        room.DisplayOrder = request.DisplayOrder;
        room.CanvasWidth = request.CanvasWidth;
        room.CanvasHeight = request.CanvasHeight;

        await context.SaveChangesAsync(cancellationToken);

        return Result<RoomDto>.Success(new RoomDto(
            room.Id,
            room.Name,
            room.DisplayOrder,
            room.CanvasWidth,
            room.CanvasHeight,
            []));
    }
}
