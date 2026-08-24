using DigitalRegistry.Application.UnitTests.TestDoubles;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DigitalRegistry.Application.UnitTests.Persistence;

/// <summary>
/// Guards the trap that adding a child to an already-tracked parent falls into.
/// </summary>
/// <remarks>
/// <see cref="Domain.Common.BaseEntity"/> gives every entity its key the moment it is constructed. A
/// child discovered on a tracked parent's collection therefore looks to EF Core like a row that
/// already exists, and it issues an <c>UPDATE</c> matching nothing rather than an <c>INSERT</c>. It
/// surfaces as a concurrency failure, which points nowhere near the cause.
/// <para>
/// Two handlers hit this — adding a line to an open tab, and replacing a recipe — and both are fixed
/// the same way: hand the child to its <c>DbSet</c>, not only to the parent's collection. These tests
/// assert the entity state directly, because the in-memory provider does not enforce the row count
/// that makes the bug visible against SQL Server.
/// </para>
/// </remarks>
public class TrackedChildInsertTests
{
    [Fact]
    public async Task AddingALineToATrackedOrder_IsOnlyAddedOnceItReachesTheSet()
    {
        await using var context = TestDbContextFactory.Create();
        var (order, menuItem) = await SeedAsync(context);

        var tracked = await context.Orders
            .Include(candidate => candidate.OrderItems)
            .SingleAsync(candidate => candidate.Id == order.Id);

        var line = tracked.AddItem(menuItem, 1);

        // Reached only through the parent's collection, EF Core reads the pre-set key as an existing
        // row. This is the state that produces the UPDATE-matching-nothing failure.
        context.ChangeTracker.DetectChanges();
        Assert.NotEqual(EntityState.Added, context.Entry(line).State);

        context.OrderItems.Add(line);

        Assert.Equal(EntityState.Added, context.Entry(line).State);

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Equal(2, await context.OrderItems.CountAsync(item => item.OrderId == order.Id));
    }

    [Fact]
    public async Task ReplacingARecipe_RemovesTheOldLinesAndInsertsTheNew()
    {
        await using var context = TestDbContextFactory.Create();
        var (_, menuItem) = await SeedAsync(context);

        var other = new Ingredient
        {
            Name = "Milk",
            Unit = UnitOfMeasure.Milliliters,
            StockQuantity = 5000m,
            LowStockThreshold = 500m
        };
        context.Ingredients.Add(other);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // The shape SetRecipeCommandHandler uses: work through the set, never the navigation.
        var existing = await context.RecipeItems
            .Where(line => line.MenuItemId == menuItem.Id)
            .ToListAsync();

        context.RecipeItems.RemoveRange(existing);
        context.RecipeItems.Add(new RecipeItem
        {
            MenuItemId = menuItem.Id,
            IngredientId = other.Id,
            QuantityRequired = 120m
        });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var recipe = await context.RecipeItems
            .Where(line => line.MenuItemId == menuItem.Id)
            .ToListAsync();

        var line2 = Assert.Single(recipe);
        Assert.Equal(other.Id, line2.IngredientId);
        Assert.Equal(120m, line2.QuantityRequired);
    }

    private static async Task<(Order Order, MenuItem MenuItem)> SeedAsync(
        Infrastructure.Persistence.ApplicationDbContext context)
    {
        var beans = new Ingredient
        {
            Name = "Espresso beans",
            Unit = UnitOfMeasure.Grams,
            StockQuantity = 5000m,
            LowStockThreshold = 500m
        };

        var menuItem = new MenuItem { Name = "Espresso", Category = "Coffee", UnitPrice = 180m };
        menuItem.Recipe.Add(new RecipeItem { Ingredient = beans, QuantityRequired = 18m });

        var table = new Table { TableNumber = 1, Capacity = 4 };

        context.Ingredients.Add(beans);
        context.MenuItems.Add(menuItem);
        context.Tables.Add(table);
        await context.SaveChangesAsync();

        var order = Order.OpenForTable(table, Guid.NewGuid());
        context.Orders.Add(order);
        order.AddItem(menuItem, 1);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return (order, menuItem);
    }
}
