using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Inventory;
using DigitalRegistry.Application.Features.Inventory.Commands.RestockIngredient;
using DigitalRegistry.Application.Features.Inventory.Queries.GetLowStockReport;
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
}
