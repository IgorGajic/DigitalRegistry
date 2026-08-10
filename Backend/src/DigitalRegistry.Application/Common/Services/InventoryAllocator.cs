using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Common.Services;

/// <summary>
/// Implements the deduction rule: menu item to recipe lines to ingredients.
/// </summary>
/// <remarks>
/// Deducting is all-or-nothing. Shortages are detected across the whole request before any stock
/// moves, so a partially prepared order can never leave the books inconsistent.
/// <para>
/// Availability is refreshed in the same transaction as the stock movement rather than from a
/// post-commit listener. Doing it afterwards would leave a window in which the menu still offers
/// something the kitchen can no longer make, and a guest could order it.
/// </para>
/// </remarks>
public sealed class InventoryAllocator(IDigitalRegistryDbContext context) : IInventoryAllocator
{
    public async Task<Result<IReadOnlyCollection<Guid>>> DeductAsync(
        IReadOnlyDictionary<Guid, int> servingsByMenuItemId,
        CancellationToken cancellationToken = default)
    {
        if (servingsByMenuItemId.Count == 0)
        {
            return Result<IReadOnlyCollection<Guid>>.Success([]);
        }

        var requirements = await LoadRequirementsAsync(servingsByMenuItemId.Keys, cancellationToken);

        // Total the demand per ingredient first: two different items on the same order may draw on
        // the same ingredient, and only the sum tells us whether there is enough.
        var demandByIngredient = new Dictionary<Guid, decimal>();

        foreach (var requirement in requirements)
        {
            var servings = servingsByMenuItemId[requirement.MenuItemId];
            var required = requirement.QuantityRequired * servings;

            demandByIngredient[requirement.Ingredient.Id] =
                demandByIngredient.GetValueOrDefault(requirement.Ingredient.Id) + required;
        }

        var shortages = requirements
            .Select(requirement => requirement.Ingredient)
            .DistinctBy(ingredient => ingredient.Id)
            .Where(ingredient => !ingredient.HasStockFor(demandByIngredient[ingredient.Id]))
            .Select(ingredient =>
                $"'{ingredient.Name}' requires {demandByIngredient[ingredient.Id]} {ingredient.Unit} "
                + $"but only {ingredient.StockQuantity} is in stock.")
            .ToArray();

        if (shortages.Length > 0)
        {
            return Result<IReadOnlyCollection<Guid>>.Conflict(string.Join(" ", shortages));
        }

        foreach (var ingredient in requirements
                     .Select(requirement => requirement.Ingredient)
                     .DistinctBy(ingredient => ingredient.Id))
        {
            // Raises IngredientLowStockDomainEvent when this takes it to or below its threshold.
            ingredient.Deduct(demandByIngredient[ingredient.Id]);
        }

        return Result<IReadOnlyCollection<Guid>>.Success(demandByIngredient.Keys.ToArray());
    }

    public async Task<IReadOnlyCollection<Guid>> ReturnAsync(
        IReadOnlyDictionary<Guid, int> servingsByMenuItemId,
        CancellationToken cancellationToken = default)
    {
        if (servingsByMenuItemId.Count == 0)
        {
            return [];
        }

        var requirements = await LoadRequirementsAsync(servingsByMenuItemId.Keys, cancellationToken);
        var returnedIngredientIds = new HashSet<Guid>();

        foreach (var requirement in requirements)
        {
            var servings = servingsByMenuItemId[requirement.MenuItemId];
            requirement.Ingredient.Restock(requirement.QuantityRequired * servings);
            returnedIngredientIds.Add(requirement.Ingredient.Id);
        }

        return returnedIngredientIds;
    }

    public async Task RefreshMenuAvailabilityAsync(
        IReadOnlyCollection<Guid> ingredientIds,
        CancellationToken cancellationToken = default)
    {
        if (ingredientIds.Count == 0)
        {
            return;
        }

        // Every menu item touching any of these ingredients, with its full recipe: an item is only
        // makeable if all of its ingredients are in stock, not just the ones that moved.
        var affectedMenuItems = await context.MenuItems
            .Where(menuItem => menuItem.Recipe.Any(recipeItem => ingredientIds.Contains(recipeItem.IngredientId)))
            .Include(menuItem => menuItem.Recipe)
            .ThenInclude(recipeItem => recipeItem.Ingredient)
            .ToListAsync(cancellationToken);

        foreach (var menuItem in affectedMenuItems)
        {
            // Raises MenuItemAvailabilityChangedDomainEvent only when the flag actually flips, so
            // clients are not told the same thing repeatedly.
            menuItem.SetAvailability(menuItem.CanBePreparedFromStock());
        }
    }

    /// <summary>
    /// Loads the recipe lines for the given menu items with their ingredients attached and tracked,
    /// so adjusting stock is picked up by the caller's <c>SaveChanges</c>.
    /// </summary>
    private async Task<List<RecipeRequirement>> LoadRequirementsAsync(
        IEnumerable<Guid> menuItemIds,
        CancellationToken cancellationToken)
    {
        var ids = menuItemIds.ToArray();

        return await context.RecipeItems
            .Where(recipeItem => ids.Contains(recipeItem.MenuItemId))
            .Select(recipeItem => new RecipeRequirement
            {
                MenuItemId = recipeItem.MenuItemId,
                QuantityRequired = recipeItem.QuantityRequired,
                Ingredient = recipeItem.Ingredient!
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>One ingredient demand of one menu item.</summary>
    private sealed class RecipeRequirement
    {
        public Guid MenuItemId { get; init; }

        public decimal QuantityRequired { get; init; }

        public required Ingredient Ingredient { get; init; }
    }
}
