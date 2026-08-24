using DigitalRegistry.Application.Features.FloorPlan;
using DigitalRegistry.Application.Features.Menu;
using DigitalRegistry.Application.Features.Orders;
using DigitalRegistry.Application.Features.Orders.Commands.CreateOrder;
using DigitalRegistry.Application.Features.Orders.Commands.ProcessPayment;
using DigitalRegistry.Application.Features.Orders.Commands.UpdateOrderItem;
using DigitalRegistry.Application.Features.Orders.Commands.VoidOrderItem;
using DigitalRegistry.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DigitalRegistry.IntegrationTests;

/// <summary>
/// The evening a table has: open a tab, add to it, cancel part of it, and settle it.
/// </summary>
/// <remarks>
/// Written as one test rather than several because it is one story, and each step only means
/// anything given the ones before it. What it is really checking is that the pieces agree: the order
/// total, the stock the recipes consume, the ledger that explains the stock, and the transaction
/// that closes the bill.
/// </remarks>
public class TillFlowTests : IClassFixture<DigitalRegistryApiFactory>
{
    private readonly DigitalRegistryApiFactory factory;

    public TillFlowTests(DigitalRegistryApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task A_tab_runs_from_first_order_to_settled_bill()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);

        var menu = await waiter.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");
        var espresso = menu!.Single(item => item.Name == "Espresso");

        var plan = await waiter.GetFromJsonAsync<FloorPlanDto>("/api/floor-plan");
        var table = plan!.Rooms.SelectMany(room => room.Tables)
            .First(candidate => candidate.OpenOrderIds.Count == 0);

        // One espresso consumes 18 g of beans, so the arithmetic below is fixed by the seeded recipe.
        var beansBefore = await StockOfAsync("Espresso beans");

        // -------------------------------------------------------------- two espressos to start
        var created = await waiter.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderCommand(table.Id, [new OrderLineRequest(espresso.Id, 2)]));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var order = (await created.Content.ReadFromJsonAsync<OrderDto>())!;

        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(360m, order.Total);
        Assert.Equal(beansBefore - 36m, await StockOfAsync("Espresso beans"));

        // The table is now occupied, and the floor plan is what tells the till which tab is running.
        var occupied = await waiter.GetFromJsonAsync<FloorPlanDto>("/api/floor-plan");
        var sameTable = occupied!.Rooms.SelectMany(room => room.Tables)
            .Single(candidate => candidate.Id == table.Id);

        Assert.Equal(TableStatus.Occupied, sameTable.Status);
        Assert.Equal([order.Id], sameTable.OpenOrderIds);

        // ------------------------------------------------------------------- a third, same tab
        var itemId = order.Items.Single().Id;

        var increased = await waiter.PatchAsJsonAsync(
            $"/api/orders/{order.Id}/items",
            new UpdateOrderItemCommand(order.Id, OrderItemChange.IncreaseQuantity, itemId, Quantity: 3));

        increased.EnsureSuccessStatusCode();

        Assert.Equal(540m, (await increased.Content.ReadFromJsonAsync<OrderDto>())!.Total);
        Assert.Equal(beansBefore - 54m, await StockOfAsync("Espresso beans"));

        // ------------------------------------------------------ one sent back, with a reason
        var voided = await waiter.PostAsJsonAsync(
            $"/api/orders/{order.Id}/items/{itemId}/void",
            new VoidOrderItemCommand(order.Id, itemId, "Hladan espreso", Quantity: 1));

        voided.EnsureSuccessStatusCode();

        var voidResult = (await voided.Content.ReadFromJsonAsync<VoidResultDto>())!;

        Assert.Equal(VoidType.Item, voidResult.Type);
        Assert.Equal(180m, voidResult.Amount);
        Assert.Equal(360m, voidResult.RemainingTotal);

        // What was made and sent back is stock the kitchen gets to keep.
        Assert.Equal(beansBefore - 36m, await StockOfAsync("Espresso beans"));

        var record = await factory.QueryAsync(context => context.VoidRecords
            .IgnoreQueryFilters()
            .SingleAsync(entry => entry.OrderId == order.Id));

        Assert.Equal("Hladan espreso", record.Reason);
        Assert.Equal("Espresso", record.ItemName);
        Assert.Equal(1, record.Quantity);

        // ---------------------------------------------------------------------------- settled
        var paid = await waiter.PostAsJsonAsync(
            $"/api/orders/{order.Id}/payment",
            new ProcessPaymentCommand(order.Id, PaymentMethod.Cash));

        paid.EnsureSuccessStatusCode();

        var transaction = (await paid.Content.ReadFromJsonAsync<TransactionDto>())!;

        Assert.Equal(360m, transaction.Amount);
        Assert.Equal(PaymentMethod.Cash, transaction.PaymentMethod);

        var settled = await factory.QueryAsync(context => context.Orders
            .IgnoreQueryFilters()
            .SingleAsync(candidate => candidate.Id == order.Id));

        Assert.Equal(OrderStatus.Paid, settled.Status);

        // The ledger has to explain the balance, not just agree with it.
        var movements = await factory.QueryAsync(context => context.StockMovements
            .IgnoreQueryFilters()
            .Where(movement => movement.OrderId == order.Id)
            .ToListAsync());

        Assert.Equal(-54m, movements.Where(m => m.Type == StockMovementType.Sale).Sum(m => m.Quantity));
        Assert.Equal(18m, movements.Where(m => m.Type == StockMovementType.Return).Sum(m => m.Quantity));

        // And the table is free again.
        var afterwards = await waiter.GetFromJsonAsync<FloorPlanDto>("/api/floor-plan");
        var freed = afterwards!.Rooms.SelectMany(room => room.Tables)
            .Single(candidate => candidate.Id == table.Id);

        Assert.Empty(freed.OpenOrderIds);
        Assert.Equal(TableStatus.Available, freed.Status);
    }

    [Fact]
    public async Task A_settled_bill_is_reversed_only_by_a_manager()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);

        var menu = await waiter.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");
        var cappuccino = menu!.Single(item => item.Name == "Cappuccino");

        var plan = await waiter.GetFromJsonAsync<FloorPlanDto>("/api/floor-plan");
        var table = plan!.Rooms.SelectMany(room => room.Tables)
            .First(candidate => candidate.OpenOrderIds.Count == 0);

        var created = await waiter.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderCommand(table.Id, [new OrderLineRequest(cappuccino.Id, 1)]));

        var order = (await created.Content.ReadFromJsonAsync<OrderDto>())!;

        await waiter.PostAsJsonAsync(
            $"/api/orders/{order.Id}/payment",
            new ProcessPaymentCommand(order.Id, PaymentMethod.Card));

        // The waiter who took the money is not the one who can take it back.
        var refused = await waiter.PostAsJsonAsync(
            $"/api/orders/{order.Id}/reverse",
            new { orderId = order.Id, reason = "Pogresno naplaceno gostu" });

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var reversed = await manager.PostAsJsonAsync(
            $"/api/orders/{order.Id}/reverse",
            new { orderId = order.Id, reason = "Pogresno naplaceno gostu" });

        reversed.EnsureSuccessStatusCode();

        var transactions = await factory.QueryAsync(context => context.Transactions
            .IgnoreQueryFilters()
            .Where(entry => entry.OrderId == order.Id)
            .ToListAsync());

        // A payment and a counter-entry, not an edited payment: the takings must show both halves.
        Assert.Equal(2, transactions.Count);
        Assert.Equal(0m, transactions.Sum(entry => entry.Amount));
        Assert.Single(transactions, entry => entry.IsReversal);

        var status = await factory.QueryAsync(context => context.Orders
            .IgnoreQueryFilters()
            .Where(candidate => candidate.Id == order.Id)
            .Select(candidate => candidate.Status)
            .SingleAsync());

        Assert.Equal(OrderStatus.Voided, status);
    }

    [Fact]
    public async Task A_manager_cannot_open_a_tab()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);

        var plan = await manager.GetFromJsonAsync<FloorPlanDto>("/api/floor-plan");
        var table = plan!.Rooms.SelectMany(room => room.Tables).First();

        var menu = await manager.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");

        var response = await manager.PostAsJsonAsync(
            "/api/orders",
            new CreateOrderCommand(table.Id, [new OrderLineRequest(menu!.First().Id, 1)]));

        // Not an oversight: the matrix gives serving to waiters and owners, and the till has to hold
        // that line rather than trust the screen not to offer the button.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<decimal> StockOfAsync(string ingredient) =>
        factory.QueryAsync(context => context.Ingredients
            .IgnoreQueryFilters()
            .Where(candidate => candidate.Name == ingredient)
            .Select(candidate => candidate.StockQuantity)
            .SingleAsync());
}
