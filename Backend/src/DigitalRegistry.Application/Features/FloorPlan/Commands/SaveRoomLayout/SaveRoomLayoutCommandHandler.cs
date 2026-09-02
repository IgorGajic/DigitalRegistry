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

        var existingFixtures = await context.RoomFixtures
            .Where(fixture => fixture.RoomId == room.Id)
            .ToListAsync(cancellationToken);

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

        if (ApplyFixtures(room, request.Fixtures, existingFixtures) is { } fixtureProblem)
        {
            return fixtureProblem;
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

        var savedFixtures = await context.RoomFixtures
            .AsNoTracking()
            .Where(fixture => fixture.RoomId == room.Id)
            .OrderBy(fixture => fixture.DisplayOrder)
            .Select(fixture => new RoomFixtureDto(
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
                DisplayOrder: fixture.DisplayOrder))
            .ToListAsync(cancellationToken);

        return Result<RoomDto>.Success(new RoomDto(
            room.Id,
            room.Name,
            room.DisplayOrder,
            room.CanvasWidth,
            room.CanvasHeight,
            saved,
            savedFixtures));
    }

    /// <summary>
    /// Brings the room's fixtures in line with what the editor sent: new ones added, known ones
    /// moved, and anything left out removed. Returns the failure to report, or null if all is well.
    /// </summary>
    /// <remarks>
    /// Nothing is written until the caller saves, so a fixture that does not fit aborts the whole
    /// request — the arrangement is stored as the owner arranged it, or not at all, tables included.
    /// </remarks>
    private Result<RoomDto>? ApplyFixtures(
        Domain.Entities.Room room,
        IReadOnlyList<FixtureLayoutRequest> requested,
        List<Domain.Entities.RoomFixture> existing)
    {
        var byId = existing.ToDictionary(fixture => fixture.Id);
        var keptIds = new HashSet<Guid>();

        foreach (var layout in requested)
        {
            if (layout.PositionX + layout.Width > room.CanvasWidth
                || layout.PositionY + layout.Height > room.CanvasHeight)
            {
                return Result<RoomDto>.Invalid(
                    $"\"{layout.Label}\" does not fit inside the room's {room.CanvasWidth}"
                    + $"×{room.CanvasHeight} area.");
            }

            Domain.Entities.RoomFixture fixture;

            if (layout.Id is { } id)
            {
                // Loaded through the tenant filter and restricted to this room, so an id naming
                // another restaurant's fixture — or another room's — simply is not here.
                if (!byId.TryGetValue(id, out var found))
                {
                    return Result<RoomDto>.NotFound(
                        "The layout refers to a fixture that does not belong to this room.");
                }

                fixture = found;
                keptIds.Add(id);
            }
            else
            {
                fixture = new Domain.Entities.RoomFixture
                {
                    RestaurantId = room.RestaurantId,
                    RoomId = room.Id
                };

                context.RoomFixtures.Add(fixture);
            }

            fixture.Kind = layout.Kind;
            fixture.Label = layout.Label.Trim();
            fixture.Shape = layout.Shape;
            fixture.Tone = layout.Tone;
            fixture.PositionX = layout.PositionX;
            fixture.PositionY = layout.PositionY;
            fixture.Width = layout.Width;
            fixture.Height = layout.Height;
            fixture.Rotation = layout.Rotation;
            fixture.DisplayOrder = layout.DisplayOrder;
        }

        // Absent means deleted here, unlike a table, which is only taken out of the room.
        context.RoomFixtures.RemoveRange(
            existing.Where(fixture => !keptIds.Contains(fixture.Id)));

        return null;
    }
}
