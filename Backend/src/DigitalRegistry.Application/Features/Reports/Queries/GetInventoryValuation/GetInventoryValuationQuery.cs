using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetInventoryValuation;

/// <summary>
/// What the store holds, what it is worth, and what moved through it over a period.
/// </summary>
/// <remarks>
/// The quantity and value are as they stand now; the consumption and purchase figures are for the
/// period. Mixing the two is deliberate — an owner asking "what have we got and what did we get
/// through" wants both on one sheet.
/// </remarks>
/// <param name="LowStockOnly">Narrows to ingredients at or below their reorder threshold.</param>
public record GetInventoryValuationQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    bool LowStockOnly = false) : IRequest<Result<InventoryValuationDto>>;
