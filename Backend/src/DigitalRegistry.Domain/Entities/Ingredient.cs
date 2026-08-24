using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Domain.Exceptions;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A stocked raw material consumed by menu item recipes.
/// </summary>
public class Ingredient : AggregateRoot, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal StockQuantity { get; set; }

    public UnitOfMeasure Unit { get; set; }

    public decimal LowStockThreshold { get; set; }

    /// <summary>
    /// What a unit has cost on average, weighted by how much arrived at each price.
    /// </summary>
    /// <remarks>
    /// A moving average rather than the last price paid: stock on the shelf is a mixture of
    /// deliveries, and valuing all of it at whatever the most recent invoice happened to say would
    /// swing the store's worth on every purchase. Zero until the first delivery is recorded.
    /// </remarks>
    public decimal AveragePurchasePrice { get; set; }

    /// <summary>What the quantity on hand is worth, at the average price paid for it.</summary>
    public decimal StockValue => decimal.Round(StockQuantity * AveragePurchasePrice, 2);

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

    /// <summary>Returns stock, for example when an order line is cancelled.</summary>
    /// <remarks>
    /// Deliberately leaves <see cref="AveragePurchasePrice"/> alone: stock coming back was already
    /// bought and already priced. Only a delivery changes what a unit has cost — see
    /// <see cref="Receive"/>.
    /// </remarks>
    public void Restock(decimal quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException("Restocked quantity must be greater than zero.");
        }

        StockQuantity += quantity;
    }

    /// <summary>
    /// Takes in a delivery, folding its price into the moving average.
    /// </summary>
    /// <remarks>
    /// The new average weights what is already on the shelf against what has just arrived. When the
    /// shelf is empty the delivery simply sets the price, since there is nothing to average against.
    /// </remarks>
    public void Receive(decimal quantity, decimal purchaseUnitPrice)
    {
        if (quantity <= 0)
        {
            throw new DomainException("A delivery must be for more than zero.");
        }

        if (purchaseUnitPrice < 0)
        {
            throw new DomainException("A purchase price cannot be negative.");
        }

        AveragePurchasePrice = StockQuantity > 0
            ? decimal.Round(
                ((StockQuantity * AveragePurchasePrice) + (quantity * purchaseUnitPrice))
                / (StockQuantity + quantity),
                4)
            : purchaseUnitPrice;

        StockQuantity += quantity;
    }

    /// <summary>
    /// Corrects the quantity on hand to what a stocktake actually found.
    /// </summary>
    /// <remarks>
    /// The one operation that may move stock either way and is not driven by a sale or a delivery,
    /// which is why the caller must say why. The average price is untouched: a count finding less
    /// than expected does not change what the missing stock cost.
    /// </remarks>
    /// <returns>The difference applied — negative when the count came up short.</returns>
    public decimal AdjustTo(decimal countedQuantity)
    {
        if (countedQuantity < 0)
        {
            throw new DomainException("A stocktake cannot find less than nothing.");
        }

        var difference = countedQuantity - StockQuantity;

        StockQuantity = countedQuantity;

        if (IsLowOnStock && difference < 0)
        {
            RaiseDomainEvent(new IngredientLowStockDomainEvent(Id, Name, StockQuantity, LowStockThreshold));
        }

        return difference;
    }
}
