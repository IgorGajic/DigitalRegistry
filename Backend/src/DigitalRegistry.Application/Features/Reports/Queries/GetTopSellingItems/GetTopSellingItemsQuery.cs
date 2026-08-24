using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetTopSellingItems;

/// <summary>
/// What sold over a period, by quantity and by what it brought in.
/// </summary>
/// <remarks>
/// Counts only what was actually paid for. Lines on cancelled or reversed bills are excluded, so the
/// ranking cannot be inflated by ringing items up and voiding them.
/// </remarks>
/// <param name="Category">Narrows to one part of the menu.</param>
/// <param name="Top">How many rows to return.</param>
public record GetTopSellingItemsQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    string? Category = null,
    int Top = 20) : IRequest<Result<IReadOnlyList<TopSellingItemDto>>>;
