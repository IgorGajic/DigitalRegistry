using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Inventory.Queries.GetStockEntries;

/// <summary>Deliveries received over a period, newest first.</summary>
public record GetStockEntriesQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? IngredientId = null) : IRequest<Result<IReadOnlyList<StockEntryDto>>>;
