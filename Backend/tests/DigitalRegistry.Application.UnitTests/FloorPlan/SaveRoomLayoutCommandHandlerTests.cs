using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Features.FloorPlan;
using DigitalRegistry.Application.Features.FloorPlan.Commands.SaveRoomLayout;
using DigitalRegistry.Application.UnitTests.TestDoubles;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DigitalRegistry.Application.UnitTests.FloorPlan;

/// <summary>
/// Saving a room's arrangement as one atomic replacement.
/// </summary>
public class SaveRoomLayoutCommandHandlerTests
{
    [Fact]
    public async Task Save_PlacesTablesInTheRoom()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, tables) = await SeedAsync(context, tableCount: 2);

        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id,
        [
            Layout(tables[0].Id, 100, 200),
            Layout(tables[1].Id, 300, 400)
        ]));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Value.Tables.Count);

        var stored = await context.Tables.OrderBy(table => table.TableNumber).ToListAsync();
        Assert.All(stored, table => Assert.Equal(room.Id, table.RoomId));
        Assert.Equal(100, stored[0].PositionX);
        Assert.Equal(400, stored[1].PositionY);
    }

    [Fact]
    public async Task Save_TakesOmittedTablesOutOfTheRoom()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, tables) = await SeedAsync(context, tableCount: 2);

        await Handle(context, new SaveRoomLayoutCommand(room.Id,
        [
            Layout(tables[0].Id, 100, 100),
            Layout(tables[1].Id, 200, 200)
        ]));

        // Dragging a table out of the room is expressed by leaving it out of the next save.
        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id,
        [
            Layout(tables[0].Id, 100, 100)
        ]));

        Assert.True(result.Succeeded);
        Assert.Single(result.Value.Tables);

        var removed = await context.Tables.SingleAsync(table => table.Id == tables[1].Id);
        Assert.Null(removed.RoomId);
    }

    [Fact]
    public async Task Save_RefusesATableThatWouldFallOutsideTheRoom()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, tables) = await SeedAsync(context, tableCount: 1);

        // The room is 1200 wide; an 80-wide table at x=1150 would hang over the edge.
        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id,
        [
            Layout(tables[0].Id, 1150, 100)
        ]));

        Assert.False(result.Succeeded);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);

        var untouched = await context.Tables.SingleAsync();
        Assert.Null(untouched.RoomId);
    }

    [Fact]
    public async Task Save_RefusesATableFromAnotherRestaurant()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, _) = await SeedAsync(context, tableCount: 1);

        // The tenant filter hides it, so the layout cannot reach across restaurants to move it.
        var foreignTable = new Table
        {
            RestaurantId = Guid.NewGuid(),
            TableNumber = 99,
            Capacity = 4
        };

        context.Tables.Add(foreignTable);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id,
        [
            Layout(foreignTable.Id, 100, 100)
        ]));

        Assert.False(result.Succeeded);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Save_AnEmptyLayoutEmptiesTheRoom()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, tables) = await SeedAsync(context, tableCount: 2);

        await Handle(context, new SaveRoomLayoutCommand(room.Id,
        [
            Layout(tables[0].Id, 100, 100),
            Layout(tables[1].Id, 200, 200)
        ]));

        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id, []));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value.Tables);
        Assert.Equal(2, await context.Tables.CountAsync(table => table.RoomId == null));
    }

    private static TableLayoutRequest Layout(Guid tableId, int x, int y) =>
        new(tableId, x, y, Width: 80, Height: 80, Shape: TableShape.Round, Rotation: 0);

    private static Task<Result<RoomDto>> Handle(ApplicationDbContext context, SaveRoomLayoutCommand command) =>
        new SaveRoomLayoutCommandHandler(context).Handle(command, CancellationToken.None);

    private static async Task<(Room Room, List<Table> Tables)> SeedAsync(
        ApplicationDbContext context,
        int tableCount)
    {
        var room = new Room { Name = "Sala" };

        var tables = Enumerable.Range(1, tableCount)
            .Select(number => new Table { TableNumber = number, Capacity = 4 })
            .ToList();

        context.Rooms.Add(room);
        context.Tables.AddRange(tables);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return (room, tables);
    }
}
