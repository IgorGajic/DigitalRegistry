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
        ], []));

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
        ], []));

        // Dragging a table out of the room is expressed by leaving it out of the next save.
        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id,
        [
            Layout(tables[0].Id, 100, 100)
        ], []));

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
        ], []));

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
        ], []));

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
        ], []));

        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id, [], []));

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value.Tables);
        Assert.Equal(2, await context.Tables.CountAsync(table => table.RoomId == null));
    }

    // ------------------------------------------------------------------------------- fixtures
    //
    // Landmarks ride on the same save as the tables, but answer absence differently: a table left
    // out of the layout is taken out of the room, a fixture left out is gone.

    [Fact]
    public async Task Save_DrawsANewFixture()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, _) = await SeedAsync(context, tableCount: 0);

        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id, [],
        [
            Fixture(null, "Šank", 40, 40, width: 400, height: 60)
        ]));

        Assert.True(result.Succeeded);

        var stored = Assert.Single(await context.RoomFixtures.ToListAsync());
        Assert.Equal("Šank", stored.Label);
        Assert.Equal(room.Id, stored.RoomId);
        Assert.Equal(400, stored.Width);

        // Stamped with the tenant like everything else, or the next request would not see it.
        Assert.Equal(room.RestaurantId, stored.RestaurantId);
    }

    [Fact]
    public async Task Save_MovesAFixtureItAlreadyKnows()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, _) = await SeedAsync(context, tableCount: 0);

        var created = await Handle(context, new SaveRoomLayoutCommand(room.Id, [],
        [
            Fixture(null, "Šank", 40, 40)
        ]));

        var id = created.Value.Fixtures.Single().Id;

        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id, [],
        [
            Fixture(id, "Šank", 500, 300)
        ]));

        Assert.True(result.Succeeded);

        var stored = Assert.Single(await context.RoomFixtures.ToListAsync());
        Assert.Equal(id, stored.Id);
        Assert.Equal(500, stored.PositionX);
        Assert.Equal(300, stored.PositionY);
    }

    [Fact]
    public async Task Save_DeletesAFixtureLeftOut()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, _) = await SeedAsync(context, tableCount: 0);

        await Handle(context, new SaveRoomLayoutCommand(room.Id, [],
        [
            Fixture(null, "Šank", 40, 40),
            Fixture(null, "WC", 600, 40)
        ]));

        var kept = (await context.RoomFixtures.SingleAsync(fixture => fixture.Label == "Šank")).Id;

        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id, [],
        [
            Fixture(kept, "Šank", 40, 40)
        ]));

        Assert.True(result.Succeeded);

        // Not merely detached from the room, as a table would be — there is nowhere else for it to be.
        var stored = Assert.Single(await context.RoomFixtures.ToListAsync());
        Assert.Equal("Šank", stored.Label);
    }

    [Fact]
    public async Task Save_RejectsAFixtureThatFallsOutsideTheRoom()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, _) = await SeedAsync(context, tableCount: 0);

        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id, [],
        [
            Fixture(null, "Šank", room.CanvasWidth - 10, 40, width: 400, height: 60)
        ]));

        Assert.False(result.Succeeded);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);

        // Named, so the owner knows which one to move rather than hunting for it.
        Assert.Contains("Šank", string.Join(' ', result.Errors));
    }

    [Fact]
    public async Task Save_RejectsAFixtureBelongingToAnotherRoom()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, _) = await SeedAsync(context, tableCount: 0);

        var other = new Room { Name = "Bašta" };
        context.Rooms.Add(other);
        await context.SaveChangesAsync();

        await Handle(context, new SaveRoomLayoutCommand(other.Id, [],
        [
            Fixture(null, "Terasa", 40, 40)
        ]));

        var foreign = await context.RoomFixtures.SingleAsync();

        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id, [],
        [
            Fixture(foreign.Id, "Terasa", 40, 40)
        ]));

        Assert.False(result.Succeeded);
        Assert.Equal(ResultErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Save_LeavesFixturesAloneWhenOnlyTablesMove()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, tables) = await SeedAsync(context, tableCount: 1);

        var created = await Handle(context, new SaveRoomLayoutCommand(room.Id, [],
        [
            Fixture(null, "Šank", 40, 40)
        ]));

        var id = created.Value.Fixtures.Single().Id;

        // A save that carries the fixture through unchanged must not disturb it.
        var result = await Handle(context, new SaveRoomLayoutCommand(room.Id,
        [
            Layout(tables[0].Id, 100, 100)
        ],
        [
            Fixture(id, "Šank", 40, 40)
        ]));

        Assert.True(result.Succeeded);
        Assert.Single(result.Value.Tables);
        Assert.Equal(id, Assert.Single(result.Value.Fixtures).Id);
    }

    [Fact]
    public async Task DeletingARoom_TakesItsFixturesWithIt()
    {
        await using var context = TestDbContextFactory.Create();
        var (room, _) = await SeedAsync(context, tableCount: 0);

        await Handle(context, new SaveRoomLayoutCommand(room.Id, [],
        [
            Fixture(null, "Šank", 40, 40)
        ]));

        context.Rooms.Remove(await context.Rooms.SingleAsync(candidate => candidate.Id == room.Id));
        await context.SaveChangesAsync();

        Assert.Empty(await context.RoomFixtures.ToListAsync());
    }

    private static FixtureLayoutRequest Fixture(
        Guid? id,
        string label,
        int x,
        int y,
        int width = 200,
        int height = 60) =>
        new(id, FixtureKind.Bar, label, FixtureShape.Rectangle, FixtureTone.Wood,
            x, y, width, height, Rotation: 0, DisplayOrder: 0);

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
