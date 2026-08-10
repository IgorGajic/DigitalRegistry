using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Domain.ValueObjects;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// Something a guest can order, along with the recipe that says what it consumes.
/// </summary>
public class MenuItem : AggregateRoot
{
    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public bool IsAvailable { get; set; } = true;

    public ICollection<RecipeItem> Recipe { get; set; } = new List<RecipeItem>();

    public Money Price => new(UnitPrice);

    /// <summary>
    /// Turns availability on or off, raising an event only when the value actually changes so the
    /// stock guard can run on every order without spamming clients.
    /// </summary>
    /// <returns>True when the availability changed.</returns>
    public bool SetAvailability(bool isAvailable)
    {
        if (IsAvailable == isAvailable)
        {
            return false;
        }

        IsAvailable = isAvailable;
        RaiseDomainEvent(new MenuItemAvailabilityChangedDomainEvent(Id, Name, isAvailable));
        return true;
    }

    /// <summary>
    /// True when every ingredient in the recipe has enough stock for <paramref name="servings"/>.
    /// Requires <see cref="Recipe"/> and each <see cref="RecipeItem.Ingredient"/> to be loaded.
    /// </summary>
    public bool CanBePreparedFromStock(int servings = 1) =>
        Recipe.All(recipeItem =>
            recipeItem.Ingredient is null ||
            recipeItem.Ingredient.HasStockFor(recipeItem.QuantityRequired * servings));
}
