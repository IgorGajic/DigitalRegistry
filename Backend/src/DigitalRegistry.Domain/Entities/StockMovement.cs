using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// One change to an ingredient's quantity, and what caused it.
/// </summary>
/// <remarks>
/// The stock ledger. <see cref="Ingredient.StockQuantity"/> is the current balance and is what the
/// till reads; this is the history that explains how it got there. Without it "we consumed 40 litres
/// of gin this month" is unanswerable, and a cancellation putting stock back leaves no trace.
/// <para>
/// Append-only. A movement entered in error is corrected by another movement, never by editing this
/// one, so <c>SUM(Quantity)</c> over an ingredient always reconstructs its balance.
/// </para>
/// </remarks>
public class StockMovement : BaseEntity, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public Guid IngredientId { get; set; }

    public Ingredient? Ingredient { get; set; }

    public StockMovementType Type { get; set; }

    /// <summary>
    /// How much moved, signed: positive into stock, negative out of it.
    /// </summary>
    /// <remarks>
    /// Signed rather than paired with a direction flag so the ledger sums directly. The type still
    /// says why, but nothing has to interpret it to work out the arithmetic.
    /// </remarks>
    public decimal Quantity { get; set; }

    /// <summary>The ingredient's balance once this movement was applied.</summary>
    /// <remarks>
    /// Denormalised on purpose: a stock report showing a running balance would otherwise have to sum
    /// every movement ever made for each row it prints.
    /// </remarks>
    public decimal BalanceAfter { get; set; }

    /// <summary>The order that consumed or returned it, for a sale or a return.</summary>
    public Guid? OrderId { get; set; }

    public Order? Order { get; set; }

    /// <summary>The delivery it arrived on, for a purchase.</summary>
    public Guid? StockEntryId { get; set; }

    public StockEntry? StockEntry { get; set; }

    /// <summary>Why, for an adjustment. Required for those; usually null otherwise.</summary>
    public string? Note { get; set; }

    /// <summary>Who caused it, where a person did. Null for movements a sale drove automatically.</summary>
    public Guid? RecordedByUserId { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
