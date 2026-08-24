using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Inventory.Commands.RecordStockEntry;

/// <summary>
/// Records a delivery into the store, with what it cost.
/// </summary>
/// <remarks>
/// Supersedes the bare restock, which moved quantity but recorded no price and left no ledger line.
/// The price is what makes the store worth anything on a report and what lets an owner see the margin
/// between what a drink costs to pour and what it sells for.
/// </remarks>
/// <param name="TotalCost">
/// What the invoice actually says, where that differs from quantity times unit price — a rounding, or
/// a discount the unit price does not show. Left null it is computed.
/// </param>
/// <param name="EntryDateUtc">
/// When the goods arrived, where that is not today. Deliveries are often entered a day or two late.
/// </param>
public record RecordStockEntryCommand(
    Guid IngredientId,
    decimal Quantity,
    decimal PurchaseUnitPrice,
    decimal? TotalCost = null,
    string? Supplier = null,
    string? ReferenceNumber = null,
    string? Note = null,
    DateTime? EntryDateUtc = null) : IRequest<Result<StockEntryDto>>;
