using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.CreateRoom;

public class CreateRoomCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<CreateRoomCommand, Result<RoomDto>>
{
    public async Task<Result<RoomDto>> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        if (await context.Rooms.AnyAsync(room => room.Name == name, cancellationToken))
        {
            return Result<RoomDto>.Conflict($"A room called '{name}' already exists.");
        }

        var room = new Room
        {
            Name = name,
            // Appended to the end of the tab strip unless the owner says otherwise.
            DisplayOrder = request.DisplayOrder
                           ?? await context.Rooms.CountAsync(cancellationToken),
            CanvasWidth = request.CanvasWidth ?? Room.DefaultCanvasWidth,
            CanvasHeight = request.CanvasHeight ?? Room.DefaultCanvasHeight
        };

        context.Rooms.Add(room);
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
