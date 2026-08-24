using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Features.Tables;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.DeleteRoom;

public class DeleteRoomCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<DeleteRoomCommand, Result>
{
    public async Task<Result> Handle(DeleteRoomCommand request, CancellationToken cancellationToken)
    {
        var room = await context.Rooms
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (room is null)
        {
            return Result.NotFound("No such room.");
        }

        // Tearing down a room while guests are still sitting in it would take their tables off the
        // floor screen with tabs still running on them.
        var hasOpenTabs = await context.Orders
            .Where(TableStatusRules.IsOpenTab)
            .AnyAsync(order => order.Table!.RoomId == room.Id, cancellationToken);

        if (hasOpenTabs)
        {
            return Result.Conflict(
                $"'{room.Name}' still has open tabs. Settle or void them before removing the room.");
        }

        // The foreign key is SetNull, so the tables survive and become unplaced.
        context.Rooms.Remove(room);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
