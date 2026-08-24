using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Inventory.Commands.AdjustStock;
using DigitalRegistry.Application.Features.Inventory.Commands.RecordStockEntry;
using DigitalRegistry.Application.Features.Inventory.Commands.RestockIngredient;
using DigitalRegistry.Application.Features.Inventory.Queries.GetLowStockReport;
using DigitalRegistry.Application.Features.Inventory.Queries.GetStockEntries;
using DigitalRegistry.Application.Features.Inventory.Queries.GetStockMovements;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Application.Features.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// Ingredient stock levels. Manager and owner only.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.ManageInventory)]
public class InventoryController : ApiControllerBase
{
    /// <summary>Lists ingredients at or below their low-stock threshold.</summary>
    /// <response code="200">The low-stock report, most urgent first.</response>
    [HttpGet("low-stock")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<LowStockReportEntryDto>))]
    public async Task<ActionResult> GetLowStock(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetLowStockReportQuery(), cancellationToken));

    /// <summary>
    /// Adds stock to an ingredient, bringing any menu items it was blocking back on the menu.
    /// </summary>
    /// <response code="200">The ingredient's new stock position.</response>
    /// <response code="404">No ingredient with that id.</response>
    [HttpPost("ingredients/{id:guid}/restock")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IngredientStockDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Restock(
        Guid id,
        [FromBody] RestockIngredientCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.IngredientId)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The ingredient id in the route does not match the id in the body."
            });
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }

    /// <summary>Records a delivery into the store, with what it cost.</summary>
    /// <remarks>
    /// The proper way to bring stock in: it captures the purchase price, folds it into the
    /// ingredient's moving average, writes a ledger line, and brings back onto the menu anything the
    /// shortage was holding off it.
    /// </remarks>
    /// <response code="200">The delivery, with the resulting stock position.</response>
    /// <response code="404">No ingredient with that id.</response>
    [HttpPost("entries")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockEntryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> RecordEntry(
        [FromBody] RecordStockEntryCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));

    /// <summary>Lists deliveries received over a period.</summary>
    /// <param name="from">Start of the period, inclusive, in UTC.</param>
    /// <param name="to">End of the period, exclusive, in UTC.</param>
    /// <param name="ingredientId">Narrows to one ingredient.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet("entries")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<StockEntryDto>))]
    public async Task<ActionResult> GetEntries(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? ingredientId,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetStockEntriesQuery(from, to, ingredientId), cancellationToken));

    /// <summary>The stock ledger: what came in, what went out, and why.</summary>
    /// <param name="from">Start of the period, inclusive, in UTC.</param>
    /// <param name="to">End of the period, exclusive, in UTC.</param>
    /// <param name="ingredientId">Narrows to one ingredient's history.</param>
    /// <param name="type">Narrows to one kind of movement.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet("movements")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<StockMovementDto>))]
    public async Task<ActionResult> GetMovements(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? ingredientId,
        [FromQuery] StockMovementType? type,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new GetStockMovementsQuery(from, to, ingredientId, type),
            cancellationToken));

    /// <summary>Corrects an ingredient's quantity to what a stocktake found.</summary>
    /// <remarks>
    /// The only movement not driven by a sale or a delivery, so a reason is required and recorded
    /// against whoever entered it.
    /// </remarks>
    /// <response code="200">What the correction changed.</response>
    /// <response code="404">No ingredient with that id.</response>
    [HttpPost("ingredients/{id:guid}/adjust")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StockAdjustmentResultDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Adjust(
        Guid id,
        [FromBody] AdjustStockCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.IngredientId)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The ingredient id in the route does not match the id in the body."
            });
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }
}
