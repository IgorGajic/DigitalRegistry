using DigitalRegistry.Application.Common.Models;

namespace DigitalRegistry.Application.Common.Interfaces;

/// <summary>
/// Moves ingredient stock in step with what has been ordered, and keeps the menu honest about what
/// can still be made.
/// </summary>
/// <remarks>
/// Shared by every handler that changes what is on an order, so the deduction rule exists once.
/// Nothing here saves: the caller owns the unit of work, which keeps the stock movement and the
/// order change in a single transaction.
/// <para>
/// Both operations also write to the stock ledger. Doing it here rather than in each caller is what
/// makes the ledger complete: a handler that moved stock without recording why would leave the
/// consumption report quietly wrong, and nothing would reveal it.
/// </para>
/// </remarks>
public interface IInventoryAllocator
{
    /// <summary>
    /// Consumes the stock needed to prepare the requested servings.
    /// </summary>
    /// <param name="servingsByMenuItemId">How many servings of each menu item to prepare.</param>
    /// <param name="orderId">The order consuming it, recorded against each ledger movement.</param>
    /// <returns>
    /// On success, the ingredients that were touched, for passing to
    /// <see cref="RefreshMenuAvailabilityAsync"/>. A conflict result when any ingredient is short,
    /// in which case no stock has been changed.
    /// </returns>
    Task<Result<IReadOnlyCollection<Guid>>> DeductAsync(
        IReadOnlyDictionary<Guid, int> servingsByMenuItemId,
        Guid? orderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns stock, for when an order line or a whole tab is cancelled.
    /// </summary>
    /// <param name="orderId">The order it came back from, recorded against each ledger movement.</param>
    /// <returns>The ingredients that were touched.</returns>
    Task<IReadOnlyCollection<Guid>> ReturnAsync(
        IReadOnlyDictionary<Guid, int> servingsByMenuItemId,
        Guid? orderId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-evaluates every menu item whose recipe uses one of the given ingredients, taking it off the
    /// menu when it can no longer be prepared and putting it back when it can.
    /// </summary>
    Task RefreshMenuAvailabilityAsync(
        IReadOnlyCollection<Guid> ingredientIds,
        CancellationToken cancellationToken = default);
}
