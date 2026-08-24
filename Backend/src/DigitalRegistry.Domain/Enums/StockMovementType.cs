namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// Why stock moved.
/// </summary>
/// <remarks>
/// Every change to an ingredient's quantity writes one of these. The type says what happened; the
/// signed quantity on the movement says which way it went, so summing the column reconstructs the
/// balance and can be checked against what the ingredient row claims.
/// </remarks>
public enum StockMovementType
{
    /// <summary>Goods received from a supplier.</summary>
    Purchase = 1,

    /// <summary>Consumed by an order, through a menu item's recipe.</summary>
    Sale = 2,

    /// <summary>Put back after a cancellation.</summary>
    Return = 3,

    /// <summary>
    /// Corrected by hand — a stocktake, breakage, or waste.
    /// </summary>
    /// <remarks>
    /// The only type that can move stock in either direction, and the only one entered without an
    /// order or a delivery behind it. That makes it the one worth reviewing.
    /// </remarks>
    Adjustment = 4
}
