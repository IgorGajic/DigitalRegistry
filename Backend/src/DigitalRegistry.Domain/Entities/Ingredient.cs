using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Domain.Exceptions;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A stocked raw material consumed by menu item recipes.
/// </summary>
public class Ingredient : AggregateRoot
{
    public string Name { get; set; } = string.Empty;

    public decimal StockQuantity { get; set; }

    public UnitOfMeasure Unit { get; set; }

    public decimal LowStockThreshold { get; set; }

    public ICollection<RecipeItem> UsedIn { get; set; } = new List<RecipeItem>();

    /// <summary>True once stock has fallen to or below the configured threshold.</summary>
    public bool IsLowOnStock => StockQuantity <= LowStockThreshold;

    /// <summary>True when at least <paramref name="quantity"/> is physically on hand.</summary>
    public bool HasStockFor(decimal quantity) => StockQuantity >= quantity;

    /// <summary>
    /// Consumes stock as part of preparing an order.
    /// </summary>
    /// <exception cref="InsufficientStockException">
    /// Thrown rather than allowing stock to go negative.
    /// </exception>
    public void Deduct(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Deducted quantity must be greater than zero.");
        }

        if (!HasStockFor(quantity))
        {
            throw new InsufficientStockException(Name, quantity, StockQuantity);
        }

        StockQuantity -= quantity;

        if (IsLowOnStock)
        {
            RaiseDomainEvent(new IngredientLowStockDomainEvent(Id, Name, StockQuantity, LowStockThreshold));
        }
    }

    /// <summary>Returns stock, for example when an order line is reduced or removed.</summary>
    public void Restock(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Restocked quantity must be greater than zero.");
        }

        StockQuantity += quantity;
    }
}
