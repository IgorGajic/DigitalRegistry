using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetOrders;

/// <summary>
/// Lists the tabs opened in a period, newest first.
/// </summary>
/// <remarks>
/// The screen this answers is how a settled bill is found again once its receipt has been closed —
/// to reprint it, or to reverse it. Without it a paid order can only be reached by an id nobody
/// writes down.
/// </remarks>
/// <param name="From">Start of the window, inclusive. Defaults to the start of the current day.</param>
/// <param name="To">End of the window, exclusive. Defaults to now.</param>
/// <param name="Status">Restrict to one status, or null for every tab in the window.</param>
/// <param name="TableId">Restrict to one table.</param>
/// <param name="Take">Upper bound on rows returned, so a wide window cannot pull a whole year.</param>
public record GetOrdersQuery(
    DateTime? From = null,
    DateTime? To = null,
    OrderStatus? Status = null,
    Guid? TableId = null,
    int Take = 200) : IRequest<Result<IReadOnlyList<OrderSummaryDto>>>;
