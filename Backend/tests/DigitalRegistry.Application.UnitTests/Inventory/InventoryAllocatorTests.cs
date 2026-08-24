using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Common.Services;
using DigitalRegistry.Application.UnitTests.TestDoubles;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DigitalRegistry.Application.UnitTests.Inventory;

/// <summary>
/// Tests the deduction engine: that stock follows what was ordered, that shortages are all-or-nothing,
/// and that the menu is kept in step with what can actually be made.
/// </summary>
public class InventoryAllocatorTests
{
    private const decimal InitialStock = 10m;

    /// <summary>
    /// Seeds a Cheeseburger needing one patty and one bun, plus a Double burger needing two patties
    /// and one bun, so tests can exercise ingredients shared between menu items.
    /// </summary>
    private static async Task<Fixture> SeedAsync()
    {
        var context = TestDbContextFactory.Create();

        var patty = new Ingredient
        {
            Name = "Burger patty",
            StockQuantity = InitialStock,
            Unit = UnitOfMeasure.Units,
            LowStockThreshold = 2m
        };

        var bun = new Ingredient
        {
            Name = "Burger bun",
            StockQuantity = InitialStock,
            Unit = UnitOfMeasure.Units,
            LowStockThreshold = 2m
        };

        var cheeseburger = new MenuItem
        {
            Name = "Cheeseburger",
            Category = "Food",
            UnitPrice = 11.90m,
            IsAvailable = true,
            Recipe =
            {
                new RecipeItem { Ingredient = patty, QuantityRequired = 1m },
                new RecipeItem { Ingredient = bun, QuantityRequired = 1m }
            }
        };

        var doubleBurger = new MenuItem
        {
            Name = "Double burger",
            Category = "Food",
            UnitPrice = 15.90m,
            IsAvailable = true,
            Recipe =
            {
                new RecipeItem { Ingredient = patty, QuantityRequired = 2m },
                new RecipeItem { Ingredient = bun, QuantityRequired = 1m }
            }
        };

        context.MenuItems.AddRange(cheeseburger, doubleBurger);
        await context.SaveChangesAsync();

        return new Fixture(context, cheeseburger, doubleBurger, patty, bun);
    }

    [Fact]
    public async Task DeductAsync_ReducesStockByTheRecipeTimesTheServings()
    {
        var fixture = await SeedAsync();

        var result = await fixture.Allocator.DeductAsync(
            new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 3 });

        Assert.True(result.Succeeded);
        Assert.Equal(7m, fixture.Patty.StockQuantity);
        Assert.Equal(7m, fixture.Bun.StockQuantity);
        // Both touched ingredients come back, so availability can be re-checked for either.
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(fixture.Patty.Id, result.Value);
        Assert.Contains(fixture.Bun.Id, result.Value);
    }

    [Fact]
    public async Task DeductAsync_SumsDemandForAnIngredientSharedBetweenMenuItems()
    {
        var fixture = await SeedAsync();

        var result = await fixture.Allocator.DeductAsync(new Dictionary<Guid, int>
        {
            [fixture.Cheeseburger.Id] = 1,
            [fixture.DoubleBurger.Id] = 1
        });

        Assert.True(result.Succeeded);
        // One patty for the cheeseburger plus two for the double.
        Assert.Equal(7m, fixture.Patty.StockQuantity);
        // One bun each.
        Assert.Equal(8m, fixture.Bun.StockQuantity);
    }

    [Fact]
    public async Task DeductAsync_ReportsAShortageAndMovesNoStock()
    {
        var fixture = await SeedAsync();

        var result = await fixture.Allocator.DeductAsync(
            new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 11 });

        Assert.False(result.Succeeded);
        Assert.Equal(ResultErrorType.Conflict, result.ErrorType);
        // All-or-nothing: a partly filled order must not leave the books short.
        Assert.Equal(InitialStock, fixture.Patty.StockQuantity);
        Assert.Equal(InitialStock, fixture.Bun.StockQuantity);
    }

    [Fact]
    public async Task DeductAsync_DetectsAShortageOnlyVisibleWhenDemandIsCombined()
    {
        var fixture = await SeedAsync();

        // Four cheeseburgers and four doubles need 4 + 8 = 12 patties against a stock of 10, even
        // though neither line alone would exceed it.
        var result = await fixture.Allocator.DeductAsync(new Dictionary<Guid, int>
        {
            [fixture.Cheeseburger.Id] = 4,
            [fixture.DoubleBurger.Id] = 4
        });

        Assert.False(result.Succeeded);
        Assert.Contains("Burger patty", result.Error);
        Assert.Equal(InitialStock, fixture.Patty.StockQuantity);
    }

    [Fact]
    public async Task DeductAsync_AllowsTakingExactlyTheRemainingStock()
    {
        var fixture = await SeedAsync();

        var result = await fixture.Allocator.DeductAsync(
            new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 10 });

        Assert.True(result.Succeeded);
        Assert.Equal(0m, fixture.Patty.StockQuantity);
    }

    [Fact]
    public async Task DeductAsync_DoesNothingForAnEmptyRequest()
    {
        var fixture = await SeedAsync();

        var result = await fixture.Allocator.DeductAsync(new Dictionary<Guid, int>());

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value);
        Assert.Equal(InitialStock, fixture.Patty.StockQuantity);
    }

    [Fact]
    public async Task ReturnAsync_PutsStockBack()
    {
        var fixture = await SeedAsync();
        await fixture.Allocator.DeductAsync(new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 4 });
        Assert.Equal(6m, fixture.Patty.StockQuantity);

        var touched = await fixture.Allocator.ReturnAsync(
            new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 4 });

        Assert.Equal(InitialStock, fixture.Patty.StockQuantity);
        Assert.Equal(InitialStock, fixture.Bun.StockQuantity);
        Assert.Contains(fixture.Patty.Id, touched);
    }

    [Fact]
    public async Task RefreshMenuAvailabilityAsync_TakesAnItemOffTheMenuOnceItCannotBeMade()
    {
        var fixture = await SeedAsync();
        await fixture.Allocator.DeductAsync(new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 10 });

        await fixture.Allocator.RefreshMenuAvailabilityAsync([fixture.Patty.Id]);

        // No patties left, so neither burger can be made.
        Assert.False(fixture.Cheeseburger.IsAvailable);
        Assert.False(fixture.DoubleBurger.IsAvailable);
    }

    [Fact]
    public async Task RefreshMenuAvailabilityAsync_OnlyDisablesItemsThatActuallyRanOut()
    {
        var fixture = await SeedAsync();

        // Eight patties gone leaves two: enough for a cheeseburger, not for a double.
        await fixture.Allocator.DeductAsync(new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 8 });
        await fixture.Allocator.RefreshMenuAvailabilityAsync([fixture.Patty.Id]);

        Assert.True(fixture.Cheeseburger.IsAvailable);
        Assert.True(fixture.DoubleBurger.IsAvailable);

        await fixture.Allocator.DeductAsync(new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 1 });
        await fixture.Allocator.RefreshMenuAvailabilityAsync([fixture.Patty.Id]);

        // One patty left: still enough for a single, no longer enough for a double.
        Assert.True(fixture.Cheeseburger.IsAvailable);
        Assert.False(fixture.DoubleBurger.IsAvailable);
    }

    [Fact]
    public async Task RefreshMenuAvailabilityAsync_BringsAnItemBackOnceStockReturns()
    {
        var fixture = await SeedAsync();
        await fixture.Allocator.DeductAsync(new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 10 });
        await fixture.Allocator.RefreshMenuAvailabilityAsync([fixture.Patty.Id]);
        Assert.False(fixture.Cheeseburger.IsAvailable);

        fixture.Patty.Restock(5m);
        fixture.Bun.Restock(5m);
        await fixture.Allocator.RefreshMenuAvailabilityAsync([fixture.Patty.Id, fixture.Bun.Id]);

        Assert.True(fixture.Cheeseburger.IsAvailable);
    }

    [Fact]
    public async Task RefreshMenuAvailabilityAsync_RaisesTheChangeEventOnlyWhenTheFlagFlips()
    {
        var fixture = await SeedAsync();
        await fixture.Allocator.DeductAsync(new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 10 });

        await fixture.Allocator.RefreshMenuAvailabilityAsync([fixture.Patty.Id]);
        Assert.NotEmpty(fixture.Cheeseburger.DomainEvents);

        fixture.Cheeseburger.ClearDomainEvents();
        fixture.DoubleBurger.ClearDomainEvents();

        // Re-running the check while nothing has changed must stay silent, since it runs on every
        // order and would otherwise flood the displays.
        await fixture.Allocator.RefreshMenuAvailabilityAsync([fixture.Patty.Id]);
        Assert.Empty(fixture.Cheeseburger.DomainEvents);
    }

    [Fact]
    public async Task RefreshMenuAvailabilityAsync_IgnoresAnEmptyIngredientList()
    {
        var fixture = await SeedAsync();
        await fixture.Allocator.DeductAsync(new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 10 });

        await fixture.Allocator.RefreshMenuAvailabilityAsync([]);

        // Nothing was named, so nothing is re-evaluated.
        Assert.True(fixture.Cheeseburger.IsAvailable);
    }

    [Fact]
    public async Task DeductedStockIsPersisted()
    {
        var fixture = await SeedAsync();

        await fixture.Allocator.DeductAsync(new Dictionary<Guid, int> { [fixture.Cheeseburger.Id] = 3 });
        await fixture.Context.SaveChangesAsync();

        var stored = await fixture.Context.Ingredients
            .AsNoTracking()
            .FirstAsync(ingredient => ingredient.Id == fixture.Patty.Id);

        Assert.Equal(7m, stored.StockQuantity);
    }

    /// <summary>
    /// The seeded objects a test works with. The entities are the context's own tracked instances, so
    /// asserting on them observes exactly what the allocator changed.
    /// </summary>
    private sealed record Fixture(
        ApplicationDbContext Context,
        MenuItem Cheeseburger,
        MenuItem DoubleBurger,
        Ingredient Patty,
        Ingredient Bun)
    {
        public InventoryAllocator Allocator { get; } = new(Context, new TestDateTimeService());
    }
}
