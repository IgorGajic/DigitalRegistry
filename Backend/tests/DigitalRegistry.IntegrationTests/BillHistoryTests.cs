using DigitalRegistry.Application.Features.FloorPlan;
using DigitalRegistry.Application.Features.Menu;
using DigitalRegistry.Application.Features.Orders;
using DigitalRegistry.Application.Features.Orders.Commands.CreateOrder;
using DigitalRegistry.Application.Features.Orders.Commands.ProcessPayment;
using DigitalRegistry.Application.Features.Orders.Commands.VoidPaidOrder;
using DigitalRegistry.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DigitalRegistry.IntegrationTests;

/// <summary>
/// Finding a settled bill again after its receipt has been closed.
/// </summary>
/// <remarks>
/// Before the listing existed, a paid order could only be reached while its receipt was still on the
/// screen that took the payment. Nothing listed orders and nobody writes an id down, so a bill could
/// be neither reprinted nor reversed once that screen was gone — and a manager could not so much as
/// look at one, because the receipt sat behind the policy for taking payment.
/// </remarks>
public class BillHistoryTests : IClassFixture<DigitalRegistryApiFactory>
{
    private readonly DigitalRegistryApiFactory factory;

    public BillHistoryTests(DigitalRegistryApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task A_settled_bill_can_be_found_reprinted_and_reversed_after_its_receipt_is_closed()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);

        var menu = await waiter.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");
        var espresso = menu!.Single(item => item.Name == "Espresso");

        var plan = await waiter.GetFromJsonAsync<FloorPlanDto>("/api/floor-plan");
        var table = plan!.Rooms.SelectMany(room => room.Tables)
            .First(candidate => candidate.OpenOrderIds.Count == 0);

        var created = await waiter.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderCommand(table.Id, [new OrderLineRequest(espresso.Id, 2)]));

        var order = (await created.Content.ReadFromJsonAsync<OrderDto>())!;

        // ------------------------------------------------------ open tabs appear in the listing
        var open = await waiter.GetFromJsonAsync<List<OrderSummaryDto>>(
            $"/api/orders?status={(int)OrderStatus.Open}");

        var listedWhileOpen = open!.Single(bill => bill.Id == order.Id);

        Assert.Equal(table.TableNumber, listedWhileOpen.TableNumber);
        Assert.Equal(2, listedWhileOpen.ItemCount);
        Assert.Equal(360m, listedWhileOpen.Total);
        Assert.Null(listedWhileOpen.PaidAtUtc);
        Assert.Null(listedWhileOpen.PaymentMethod);
        Assert.False(listedWhileOpen.PlacedByGuest);
        Assert.NotNull(listedWhileOpen.ServedBy);

        // ------------------------------------------------------------------------------ settle
        var paid = await waiter.PostAsJsonAsync(
            $"/api/orders/{order.Id}/payment",
            new ProcessPaymentCommand(order.Id, PaymentMethod.Card));

        paid.EnsureSuccessStatusCode();

        // ------------------------------------- the bill is now findable by the day, and by number
        var settled = await manager.GetFromJsonAsync<List<OrderSummaryDto>>(
            $"/api/orders?status={(int)OrderStatus.Paid}");

        var bill = settled!.Single(candidate => candidate.Id == order.Id);

        Assert.Equal(PaymentMethod.Card, bill.PaymentMethod);
        Assert.NotNull(bill.PaidAtUtc);
        Assert.False(bill.IsReversed);

        // The short number is what a guest quotes over the telephone, so the list and the printed
        // receipt have to agree on it.
        var receipt = await manager.GetFromJsonAsync<ReceiptDto>($"/api/orders/{order.Id}/receipt");

        Assert.Equal(bill.Number, receipt!.Number);
        Assert.Equal(360m, receipt.Total);

        // A waiter can fetch a copy of a bill they closed themselves without fetching a manager.
        var waitersCopy = await waiter.GetAsync($"/api/orders/{order.Id}/receipt");
        Assert.Equal(HttpStatusCode.OK, waitersCopy.StatusCode);

        // ------------------------------------------------ and reversed from there, by a manager
        var reversed = await manager.PostAsJsonAsync(
            $"/api/orders/{order.Id}/reverse",
            new VoidPaidOrderCommand(order.Id, "Gost reklamirao pice, izdat povracaj novca"));

        reversed.EnsureSuccessStatusCode();

        var afterReversal = await manager.GetFromJsonAsync<List<OrderSummaryDto>>("/api/orders");
        var reversedBill = afterReversal!.Single(candidate => candidate.Id == order.Id);

        Assert.True(reversedBill.IsReversed);
        Assert.Equal(OrderStatus.Voided, reversedBill.Status);

        // The counter-entry a reversal writes carries a negative amount; reading it as the payment
        // would show the bill as settled by whatever method backed it out.
        Assert.Equal(PaymentMethod.Card, reversedBill.PaymentMethod);
    }

    [Fact]
    public async Task The_listing_narrows_by_table_and_by_period()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);

        var menu = await waiter.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");
        var espresso = menu!.Single(item => item.Name == "Espresso");

        var plan = await waiter.GetFromJsonAsync<FloorPlanDto>("/api/floor-plan");
        var table = plan!.Rooms.SelectMany(room => room.Tables)
            .First(candidate => candidate.OpenOrderIds.Count == 0);

        var created = await waiter.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderCommand(table.Id, [new OrderLineRequest(espresso.Id, 1)]));

        var order = (await created.Content.ReadFromJsonAsync<OrderDto>())!;

        var forTable = await waiter.GetFromJsonAsync<List<OrderSummaryDto>>(
            $"/api/orders?tableId={table.Id}");

        Assert.Contains(forTable!, bill => bill.Id == order.Id);
        Assert.All(forTable!, bill => Assert.Equal(table.Id, bill.TableId));

        // A window that ended before the tab was opened cannot contain it.
        var yesterday = DateTime.UtcNow.AddDays(-1);

        var before = await waiter.GetFromJsonAsync<List<OrderSummaryDto>>(
            $"/api/orders?from={yesterday.AddHours(-1):o}&to={yesterday:o}");

        Assert.DoesNotContain(before!, bill => bill.Id == order.Id);
    }

    [Fact]
    public async Task A_reversed_period_is_refused_rather_than_answered_with_nothing()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);

        var response = await manager.GetAsync(
            $"/api/orders?from={DateTime.UtcNow:o}&to={DateTime.UtcNow.AddDays(-1):o}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_table_session_cannot_read_the_venues_bills()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);
        var guest = await factory.OpenTableSessionAsync(manager);

        var response = await guest.Client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
