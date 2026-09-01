using DigitalRegistry.Application.Features.FloorPlan;
using DigitalRegistry.Application.Features.Tables;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DigitalRegistry.IntegrationTests;

/// <summary>
/// The tokens behind the printed QR codes.
/// </summary>
/// <remarks>
/// A token is a credential: whoever holds it can open an ordering session for that table. So the
/// sheet is a management response, and the floor plan every waiter has open all shift must not carry
/// one. It is grouped by room because that is how the printed sheet is cut up and taped down.
/// </remarks>
public class TableQrCodeSheetTests : IClassFixture<DigitalRegistryApiFactory>
{
    private readonly DigitalRegistryApiFactory factory;

    public TableQrCodeSheetTests(DigitalRegistryApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task The_sheet_carries_a_token_per_table_and_names_the_room_it_belongs_to()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);

        var codes = await manager.GetFromJsonAsync<List<TableQrCodeSheetEntryDto>>(
            "/api/tables/qr-codes");

        Assert.NotEmpty(codes!);
        Assert.All(codes!, entry => Assert.NotEqual(Guid.Empty, entry.QrCodeToken));
        Assert.All(codes!, entry => Assert.True(entry.IsActive));

        // Distinct tokens, or two tables would take each other's orders.
        Assert.Equal(codes!.Count, codes.Select(entry => entry.QrCodeToken).Distinct().Count());

        var placed = codes.Where(entry => entry.RoomId is not null).ToList();

        Assert.NotEmpty(placed);
        Assert.All(placed, entry => Assert.False(string.IsNullOrWhiteSpace(entry.RoomName)));
    }

    [Fact]
    public async Task The_sheet_narrows_to_one_room_because_that_is_how_it_is_taped_down()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);

        var plan = await manager.GetFromJsonAsync<FloorPlanDto>("/api/floor-plan");
        var room = plan!.Rooms.First(candidate => candidate.Tables.Count > 0);

        var codes = await manager.GetFromJsonAsync<List<TableQrCodeSheetEntryDto>>(
            $"/api/tables/qr-codes?roomId={room.Id}");

        Assert.NotEmpty(codes!);
        Assert.All(codes!, entry => Assert.Equal(room.Id, entry.RoomId));
        Assert.Equal(
            room.Tables.Where(table => table.IsActive).Select(table => table.Id).OrderBy(id => id),
            codes!.Select(entry => entry.TableId).OrderBy(id => id));
    }

    [Fact]
    public async Task A_rotated_token_makes_the_printed_code_stop_working()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);

        var before = await manager.GetFromJsonAsync<List<TableQrCodeSheetEntryDto>>(
            "/api/tables/qr-codes");

        var table = before![0];

        var rotated = await manager.PostAsJsonAsync($"/api/tables/{table.TableId}/qr-code", new { });
        rotated.EnsureSuccessStatusCode();

        var after = await manager.GetFromJsonAsync<List<TableQrCodeSheetEntryDto>>(
            "/api/tables/qr-codes");

        var same = after!.Single(entry => entry.TableId == table.TableId);

        Assert.NotEqual(table.QrCodeToken, same.QrCodeToken);

        // The old code is what somebody photographed off a table; it must no longer open a session.
        var stale = factory.CreateClient();

        var response = await stale.PostAsJsonAsync(
            "/api/tables/sessions",
            new { qrCodeToken = table.QrCodeToken });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_waiter_cannot_read_the_tokens()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);

        var response = await waiter.GetAsync("/api/tables/qr-codes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
