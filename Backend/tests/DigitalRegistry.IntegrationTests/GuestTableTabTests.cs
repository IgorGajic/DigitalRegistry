using DigitalRegistry.Application.Features.Menu;
using DigitalRegistry.Application.Features.Orders;
using DigitalRegistry.Application.Features.Orders.Commands.CreateGuestQrOrder;
using DigitalRegistry.Application.Features.Orders.Commands.ProcessPayment;
using DigitalRegistry.Application.Features.Tables;
using DigitalRegistry.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DigitalRegistry.IntegrationTests;

/// <summary>
/// What a guest ordering by QR code can see of what they have already had.
/// </summary>
/// <remarks>
/// Every round a table sends opens its own order, so nothing the guest has already been shown adds
/// up to the table's running total: after a second round they could see only the second one. The
/// table comes from the session token, never from the request, so a scanned code can reach its own
/// table and nothing else.
/// </remarks>
public class GuestTableTabTests : IClassFixture<DigitalRegistryApiFactory>
{
    private readonly DigitalRegistryApiFactory factory;

    public GuestTableTabTests(DigitalRegistryApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Successive_rounds_add_up_to_one_running_tab_for_the_table()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);
        var session = await factory.OpenTableSessionAsync(manager);

        var menu = await session.Client.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");
        var espresso = menu!.Single(item => item.Name == "Espresso");

        // Nothing ordered yet: an empty tab, not a failure.
        var empty = await session.Client.GetFromJsonAsync<TableTabDto>("/api/orders/mine");

        Assert.Equal(session.TableNumber, empty!.TableNumber);
        Assert.Empty(empty.Rounds);
        Assert.Equal(0, empty.ItemCount);
        Assert.Equal(0m, empty.Total);

        // ------------------------------------------------------------------------ first round
        var first = await session.Client.PostAsJsonAsync(
            "/api/orders/qr",
            new CreateGuestQrOrderCommand([new OrderLineRequest(espresso.Id, 2)]));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var afterFirst = await session.Client.GetFromJsonAsync<TableTabDto>("/api/orders/mine");

        Assert.Single(afterFirst!.Rounds);
        Assert.Equal(2, afterFirst.ItemCount);
        Assert.Equal(360m, afterFirst.Total);
        Assert.True(afterFirst.Rounds.Single().PlacedByGuest);

        // ---------------------------------------------------------------------- second round
        var second = await session.Client.PostAsJsonAsync(
            "/api/orders/qr",
            new CreateGuestQrOrderCommand([new OrderLineRequest(espresso.Id, 1)]));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var afterSecond = await session.Client.GetFromJsonAsync<TableTabDto>("/api/orders/mine");

        // The second round on its own would say one espresso; the table has had three.
        Assert.Equal(2, afterSecond!.Rounds.Count);
        Assert.Equal(3, afterSecond.ItemCount);
        Assert.Equal(540m, afterSecond.Total);
        Assert.Equal(
            ["Espresso"],
            afterSecond.Rounds.SelectMany(round => round.Lines).Select(line => line.MenuItemName).Distinct());
    }

    [Fact]
    public async Task A_settled_round_drops_off_the_tab_because_it_is_no_longer_owed()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);
        var session = await factory.OpenTableSessionAsync(manager);

        var menu = await session.Client.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");
        var espresso = menu!.Single(item => item.Name == "Espresso");

        var placed = await session.Client.PostAsJsonAsync(
            "/api/orders/qr",
            new CreateGuestQrOrderCommand([new OrderLineRequest(espresso.Id, 1)]));

        var order = (await placed.Content.ReadFromJsonAsync<OrderDto>())!;

        var settled = await waiter.PostAsJsonAsync(
            $"/api/orders/{order.Id}/payment",
            new ProcessPaymentCommand(order.Id, PaymentMethod.Cash));

        settled.EnsureSuccessStatusCode();

        var tab = await session.Client.GetFromJsonAsync<TableTabDto>("/api/orders/mine");

        Assert.Empty(tab!.Rounds);
        Assert.Equal(0m, tab.Total);
    }

    [Fact]
    public async Task A_table_session_sees_its_own_table_and_no_other()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);

        var codes = await manager.GetFromJsonAsync<List<TableQrCodeSheetEntryDto>>(
            "/api/tables/qr-codes");

        var first = codes![0];
        var second = codes[1];

        var atFirst = await factory.OpenTableSessionAsync(manager, first.TableId);
        var atSecond = await factory.OpenTableSessionAsync(manager, second.TableId);

        var menu = await atFirst.Client.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");
        var espresso = menu!.Single(item => item.Name == "Espresso");

        await atFirst.Client.PostAsJsonAsync(
            "/api/orders/qr",
            new CreateGuestQrOrderCommand([new OrderLineRequest(espresso.Id, 1)]));

        var otherTable = await atSecond.Client.GetFromJsonAsync<TableTabDto>("/api/orders/mine");

        Assert.Equal(second.TableId, otherTable!.TableId);
        Assert.Empty(otherTable.Rounds);
    }

    [Fact]
    public async Task Staff_have_no_table_session_so_the_endpoint_refuses_them()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);

        var response = await waiter.GetAsync("/api/orders/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_table_session_cannot_read_the_venue_s_licence_position()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);
        var session = await factory.OpenTableSessionAsync(manager);

        var response = await session.Client.GetAsync("/api/license/status");

        // A QR session is authenticated — it carries the restaurant and the guest role — so an
        // endpoint guarded by bare [Authorize] answered it, and the answer names the venue's plan,
        // its expiry and the days left. That is the venue's standing with the platform, and it does
        // not belong on a phone that has been pointed at a table.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_still_read_the_licence_position__it_is_their_only_warning()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);

        var response = await waiter.GetAsync("/api/license/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
