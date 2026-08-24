using DigitalRegistry.Application.Common.Services;
using DigitalRegistry.Application.UnitTests.TestDoubles;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DigitalRegistry.Application.UnitTests.Inventory;

/// <summary>
/// The ledger written alongside every stock movement.
/// </summary>
/// <remarks>
/// Its whole value rests on being complete. A movement that changed stock without writing a line here
/// would leave the consumption report quietly wrong, and nothing would reveal it — so the property
/// worth testing is that the ledger always sums back to the balance.
/// </remarks>
public class StockLedgerTests
{
    [Fact]
    public async Task Deduct_WritesANegativeLineNamingTheOrder()
    {
        await using var context = TestDbContextFactory.Create();
        var (allocator, espresso, beans) = await SeedAsync(context);
        var orderId = Guid.NewGuid();

        await allocator.DeductAsync(new Dictionary<Guid, int> { [espresso.Id] = 3 }, orderId);
        await context.SaveChangesAsync();

        var movement = await context.StockMovements.SingleAsync();

        Assert.Equal(StockMovementType.Sale, movement.Type);
        Assert.Equal(-54m, movement.Quantity);
        Assert.Equal(orderId, movement.OrderId);
        Assert.Equal(beans.Id, movement.IngredientId);
        Assert.Equal(4946m, movement.BalanceAfter);
    }

    [Fact]
    public async Task Return_WritesAPositiveLine()
    {
        await using var context = TestDbContextFactory.Create();
        var (allocator, espresso, _) = await SeedAsync(context);
        var orderId = Guid.NewGuid();

        await allocator.DeductAsync(new Dictionary<Guid, int> { [espresso.Id] = 3 }, orderId);
        await allocator.ReturnAsync(new Dictionary<Guid, int> { [espresso.Id] = 1 }, orderId);
        await context.SaveChangesAsync();

        var returned = await context.StockMovements
            .SingleAsync(movement => movement.Type == StockMovementType.Return);

        Assert.Equal(18m, returned.Quantity);
        Assert.Equal(4964m, returned.BalanceAfter);
    }

    [Fact]
    public async Task Ledger_SumsBackToTheBalance()
    {
        await using var context = TestDbContextFactory.Create();
        var (allocator, espresso, beans) = await SeedAsync(context);

        // A few movements in both directions, as a service would produce.
        await allocator.DeductAsync(new Dictionary<Guid, int> { [espresso.Id] = 5 }, Guid.NewGuid());
        await allocator.DeductAsync(new Dictionary<Guid, int> { [espresso.Id] = 2 }, Guid.NewGuid());
        await allocator.ReturnAsync(new Dictionary<Guid, int> { [espresso.Id] = 3 }, Guid.NewGuid());
        await context.SaveChangesAsync();

        var opening = 5000m;
        var netMovement = await context.StockMovements.SumAsync(movement => movement.Quantity);
        var current = await context.Ingredients
            .Where(ingredient => ingredient.Id == beans.Id)
            .Select(ingredient => ingredient.StockQuantity)
            .SingleAsync();

        // The property that makes the ledger trustworthy: it reconstructs the balance.
        Assert.Equal(current, opening + netMovement);
    }

    [Fact]
    public async Task Return_SumsOneLinePerIngredientEvenWhenTwoItemsShareIt()
    {
        await using var context = TestDbContextFactory.Create();
        var (allocator, espresso, beans) = await SeedAsync(context);

        // A second drink drawing on the same beans.
        var doubleShot = new MenuItem { Name = "Doppio", Category = "Coffee", UnitPrice = 260m };
        doubleShot.Recipe.Add(new RecipeItem { IngredientId = beans.Id, QuantityRequired = 36m });
        context.MenuItems.Add(doubleShot);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await allocator.ReturnAsync(new Dictionary<Guid, int>
        {
            [espresso.Id] = 1,
            [doubleShot.Id] = 1
        });

        await context.SaveChangesAsync();

        // One line saying 54 came back, not two the reader has to add up.
        var movement = await context.StockMovements.SingleAsync();
        Assert.Equal(54m, movement.Quantity);
    }

    [Fact]
    public async Task Deduct_WritesNothingWhenStockIsShort()
    {
        await using var context = TestDbContextFactory.Create();
        var (allocator, espresso, _) = await SeedAsync(context);

        // Far more than the 5000 grams on hand.
        var result = await allocator.DeductAsync(new Dictionary<Guid, int> { [espresso.Id] = 1000 });
        await context.SaveChangesAsync();

        Assert.False(result.Succeeded);
        Assert.Empty(await context.StockMovements.ToListAsync());
    }

    private static async Task<(InventoryAllocator Allocator, MenuItem Espresso, Ingredient Beans)> SeedAsync(
        Infrastructure.Persistence.ApplicationDbContext context)
    {
        var beans = new Ingredient
        {
            Name = "Espresso beans",
            Unit = UnitOfMeasure.Grams,
            StockQuantity = 5000m,
            LowStockThreshold = 500m,
            AveragePurchasePrice = 1.80m
        };

        var espresso = new MenuItem { Name = "Espresso", Category = "Coffee", UnitPrice = 180m };
        espresso.Recipe.Add(new RecipeItem { Ingredient = beans, QuantityRequired = 18m });

        context.Ingredients.Add(beans);
        context.MenuItems.Add(espresso);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return (new InventoryAllocator(context, new TestDateTimeService()), espresso, beans);
    }
}
