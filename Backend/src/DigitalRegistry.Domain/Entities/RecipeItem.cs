using DigitalRegistry.Domain.Common;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// One line of a menu item's recipe: how much of an ingredient a single serving consumes.
/// </summary>
public class RecipeItem : BaseEntity
{
    public Guid MenuItemId { get; set; }

    public Guid IngredientId { get; set; }

    /// <summary>Amount consumed per serving, in the ingredient's own unit of measure.</summary>
    public decimal QuantityRequired { get; set; }

    public MenuItem? MenuItem { get; set; }

    public Ingredient? Ingredient { get; set; }
}
