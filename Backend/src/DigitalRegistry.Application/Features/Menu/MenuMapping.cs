using DigitalRegistry.Domain.Entities;

namespace DigitalRegistry.Application.Features.Menu;

/// <summary>Projections shared by the menu management handlers.</summary>
internal static class MenuMapping
{
    /// <summary>
    /// Describes an item with its recipe, cost and margin.
    /// </summary>
    /// <remarks>
    /// Requires the recipe and its ingredients to be loaded. The cost is the sum of each line's
    /// quantity at that ingredient's moving average purchase price.
    /// </remarks>
    public static MenuItemDetailDto ToDetailDto(this MenuItem menuItem)
    {
        var recipe = menuItem.Recipe
            .Where(line => line.Ingredient is not null)
            .OrderBy(line => line.Ingredient!.Name)
            .Select(line => new RecipeLineDto(
                IngredientId: line.IngredientId,
                IngredientName: line.Ingredient!.Name,
                QuantityRequired: line.QuantityRequired,
                Unit: line.Ingredient.Unit,
                StockQuantity: line.Ingredient.StockQuantity,
                LineCost: decimal.Round(line.QuantityRequired * line.Ingredient.AveragePurchasePrice, 4)))
            .ToList();

        var cost = decimal.Round(recipe.Sum(line => line.LineCost), 2);

        // Left null rather than reported as 100% when nothing is known about cost. A margin computed
        // against a zero cost would read as pure profit, which is flattering and false.
        decimal? margin = cost > 0
            ? decimal.Round((menuItem.UnitPrice - cost) / menuItem.UnitPrice * 100m, 1)
            : null;

        return new MenuItemDetailDto(
            Id: menuItem.Id,
            Name: menuItem.Name,
            Category: menuItem.Category,
            UnitPrice: menuItem.UnitPrice,
            IsAvailable: menuItem.IsAvailable,
            CostPrice: cost,
            MarginPercent: menuItem.UnitPrice > 0 ? margin : null,
            Recipe: recipe);
    }
}
