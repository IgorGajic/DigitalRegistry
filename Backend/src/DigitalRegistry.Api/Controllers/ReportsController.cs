using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Reports.Queries.GetInventoryValuation;
using DigitalRegistry.Application.Features.Reports.Queries.GetTopSellingItems;
using DigitalRegistry.Application.Features.Reports.Queries.GetTurnoverReport;
using DigitalRegistry.Application.Features.Reports.Queries.GetVoidReport;
using DigitalRegistry.Application.Features.Reports;
using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// Reporting and auditing. Owner only, per the access matrix.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.FinancialReports)]
public class ReportsController : ApiControllerBase
{
    /// <summary>The day's takings, over a period.</summary>
    /// <remarks>
    /// Days are the venue's local business days, so a bill settled at 00:30 belongs to the night that
    /// produced it. Reversals are netted off — the turnover is what the venue actually kept.
    /// </remarks>
    /// <param name="from">First business day, inclusive.</param>
    /// <param name="to">Last business day, inclusive.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet("turnover")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TurnoverReportDto))]
    public async Task<ActionResult> GetTurnover(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetTurnoverReportQuery(from, to), cancellationToken));

    /// <summary>What sold over a period, ranked by what it brought in.</summary>
    /// <remarks>Counts only what was paid for; cancelled and reversed bills are excluded.</remarks>
    /// <param name="from">Start of the period, inclusive, in UTC.</param>
    /// <param name="to">End of the period, exclusive, in UTC.</param>
    /// <param name="category">Narrows to one part of the menu.</param>
    /// <param name="top">How many rows to return. Defaults to 20.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet("top-items")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<TopSellingItemDto>))]
    public async Task<ActionResult> GetTopItems(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? category,
        [FromQuery] int top,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new GetTopSellingItemsQuery(from, to, category, top <= 0 ? 20 : top),
            cancellationToken));

    /// <summary>What the store holds, what it is worth, and what went through it.</summary>
    /// <param name="from">Start of the period, inclusive, in UTC.</param>
    /// <param name="to">End of the period, exclusive, in UTC.</param>
    /// <param name="lowStockOnly">Narrows to ingredients at or below their reorder threshold.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet("inventory")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(InventoryValuationDto))]
    public async Task<ActionResult> GetInventoryValuation(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] bool lowStockOnly,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new GetInventoryValuationQuery(from, to, lowStockOnly),
            cancellationToken));

    /// <summary>Everything cancelled over a period, totalled by member of staff.</summary>
    /// <remarks>
    /// Voids are the easiest route for takings to leak out of a till. This is the review that makes
    /// the pattern visible — a waiter cancelling far more than their colleagues shows up here and
    /// nowhere else.
    /// </remarks>
    /// <param name="from">Start of the period, inclusive, in UTC.</param>
    /// <param name="to">End of the period, exclusive, in UTC.</param>
    /// <param name="performedByUserId">Narrows to one member of staff.</param>
    /// <param name="type">Narrows to one kind of cancellation.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet("voids")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VoidReportDto))]
    public async Task<ActionResult> GetVoids(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? performedByUserId,
        [FromQuery] VoidType? type,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new GetVoidReportQuery(from, to, performedByUserId, type),
            cancellationToken));
}
