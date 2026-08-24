using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Inventory.Commands.AdjustStock;

/// <summary>
/// Corrects an ingredient's quantity to what a stocktake actually found.
/// </summary>
/// <remarks>
/// The only way stock moves without a sale or a delivery behind it, which makes it the one worth
/// reviewing: breakage and waste are real, and so is covering a shortfall by writing it off. A reason
/// is required for that reason.
/// <para>
/// Stated as the counted quantity rather than a difference, because that is what the person holding
/// the clipboard has. Working out the difference is the system's job, not theirs.
/// </para>
/// </remarks>
public record AdjustStockCommand(
    Guid IngredientId,
    decimal CountedQuantity,
    string Reason) : IRequest<Result<StockAdjustmentResultDto>>;
