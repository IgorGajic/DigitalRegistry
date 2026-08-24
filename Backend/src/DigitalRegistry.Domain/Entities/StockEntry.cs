using DigitalRegistry.Domain.Common;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// Goods received into the store, with what they cost.
/// </summary>
/// <remarks>
/// The purchase price is why this exists as its own record rather than just a movement. It is what
/// makes the store worth anything on a report, and what lets an owner see the margin between what a
/// gin and tonic costs to pour and what it sells for.
/// </remarks>
public class StockEntry : BaseEntity, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public Guid IngredientId { get; set; }

    public Ingredient? Ingredient { get; set; }

    /// <summary>How much arrived, in the ingredient's own unit.</summary>
    public decimal Quantity { get; set; }

    /// <summary>What one unit cost, before any margin.</summary>
    public decimal PurchaseUnitPrice { get; set; }

    /// <summary>
    /// What the delivery cost in total.
    /// </summary>
    /// <remarks>
    /// Stored rather than multiplied on demand: an invoice may be rounded, or carry a discount the
    /// unit price does not show, and the figure that has to reconcile with the supplier is this one.
    /// </remarks>
    public decimal TotalCost { get; set; }

    /// <summary>Who it came from. Free text — the platform does not keep a supplier register.</summary>
    public string? Supplier { get; set; }

    /// <summary>Delivery note or invoice number, for reconciliation.</summary>
    public string? ReferenceNumber { get; set; }

    public string? Note { get; set; }

    public Guid RecordedByUserId { get; set; }

    public ApplicationUser? RecordedBy { get; set; }

    public DateTime EntryDateUtc { get; set; } = DateTime.UtcNow;
}
