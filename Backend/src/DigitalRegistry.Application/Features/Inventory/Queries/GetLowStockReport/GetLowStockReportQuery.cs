using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Inventory.Queries.GetLowStockReport;

/// <summary>
/// Lists ingredients at or below their low-stock threshold, worst first.
/// </summary>
public record GetLowStockReportQuery : IRequest<Result<IReadOnlyList<LowStockReportEntryDto>>>;
