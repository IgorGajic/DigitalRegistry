using DigitalRegistry.Application.UnitTests.TestDoubles;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DigitalRegistry.Application.UnitTests.Persistence;

/// <summary>
/// Proves that one restaurant's data is invisible to another without any handler asking for it.
/// </summary>
/// <remarks>
/// These are the tests that make the global query filter trustworthy. If they ever fail, every
/// handler in the system is leaking, because none of them filter by restaurant themselves.
/// </remarks>
public class TenantIsolationTests
{
    private static readonly Guid OtherRestaurantId = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task SaveChanges_StampsTheCurrentRestaurantOntoNewRows()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);

        context.Tables.Add(new Table { TableNumber = 1, Capacity = 4 });
        await context.SaveChangesAsync();

        var stored = await context.Tables.IgnoreQueryFilters().SingleAsync();

        Assert.Equal(tenant.RestaurantId, stored.RestaurantId);
    }

    [Fact]
    public async Task SaveChanges_KeepsARestaurantThatWasSetExplicitly()
    {
        await using var context = TestDbContextFactory.Create(out _);

        // An aggregate that propagated its own restaurant to a child must not have it overwritten by
        // the ambient tenant.
        context.Tables.Add(new Table { RestaurantId = OtherRestaurantId, TableNumber = 1, Capacity = 4 });
        await context.SaveChangesAsync();

        var stored = await context.Tables.IgnoreQueryFilters().SingleAsync();

        Assert.Equal(OtherRestaurantId, stored.RestaurantId);
    }

    [Fact]
    public async Task Query_ReturnsNothingForAnotherRestaurant()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);

        context.Tables.Add(new Table { TableNumber = 1, Capacity = 4 });
        context.MenuItems.Add(new MenuItem { Name = "Espresso", Category = "Coffee", UnitPrice = 180m });
        context.Ingredients.Add(new Ingredient
        {
            Name = "Espresso beans",
            StockQuantity = 1000m,
            Unit = UnitOfMeasure.Grams,
            LowStockThreshold = 100m
        });
        await context.SaveChangesAsync();

        // Move the very same context to a different restaurant. The model was already built and
        // cached at this point, which is exactly the case a captured tenant value would get wrong.
        tenant.RestaurantId = OtherRestaurantId;
        context.ChangeTracker.Clear();

        Assert.Empty(await context.Tables.ToListAsync());
        Assert.Empty(await context.MenuItems.ToListAsync());
        Assert.Empty(await context.Ingredients.ToListAsync());
    }

    [Fact]
    public async Task Query_ReturnsRowsAgainWhenTheOriginalRestaurantReturns()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);

        var originalRestaurantId = tenant.RestaurantId;

        context.Tables.Add(new Table { TableNumber = 1, Capacity = 4 });
        await context.SaveChangesAsync();

        tenant.RestaurantId = OtherRestaurantId;
        context.ChangeTracker.Clear();
        Assert.Empty(await context.Tables.ToListAsync());

        tenant.RestaurantId = originalRestaurantId;
        context.ChangeTracker.Clear();

        // The filter is re-evaluated per query rather than baked into the cached model.
        Assert.Single(await context.Tables.ToListAsync());
    }

    [Fact]
    public async Task IgnoreQueryFilters_IsTheOnlyWayToSeeAcrossRestaurants()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);

        context.Tables.Add(new Table { TableNumber = 1, Capacity = 4 });
        context.Tables.Add(new Table { RestaurantId = OtherRestaurantId, TableNumber = 1, Capacity = 2 });
        await context.SaveChangesAsync();

        context.ChangeTracker.Clear();

        // Both restaurants may number a table 1; the filter is what keeps them apart.
        Assert.Single(await context.Tables.ToListAsync());
        Assert.Equal(2, await context.Tables.IgnoreQueryFilters().CountAsync());

        tenant.RestaurantId = OtherRestaurantId;
        context.ChangeTracker.Clear();

        var visible = await context.Tables.SingleAsync();
        Assert.Equal(2, visible.Capacity);
    }

    [Fact]
    public async Task Restaurants_AreNotFilteredBecauseSignInHasToFindThemFirst()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);

        context.Restaurants.Add(new Restaurant { Name = "Kafana X", Slug = "kafana-x" });
        await context.SaveChangesAsync();

        tenant.RestaurantId = OtherRestaurantId;
        context.ChangeTracker.Clear();

        // A user typing a restaurant code has no token yet, so this lookup happens with no tenant at
        // all. It has to keep working.
        Assert.NotNull(await context.Restaurants.SingleOrDefaultAsync(r => r.Slug == "kafana-x"));
    }
}
