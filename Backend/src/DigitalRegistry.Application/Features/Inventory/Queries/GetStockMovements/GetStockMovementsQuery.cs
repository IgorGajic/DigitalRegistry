using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Inventory.Queries.GetStockMovements;

/// <summary>
/// The stock ledger over a period: what came in, what went out, and why.
/// </summary>
/// <param name="IngredientId">Narrows to one ingredient's history.</param>
public record GetStockMovementsQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? IngredientId = null,
    StockMovementType? Type = null) : IRequest<Result<IReadOnlyList<StockMovementDto>>>;
