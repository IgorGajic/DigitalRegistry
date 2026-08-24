using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Menu.Commands.DeleteMenuItem;
using DigitalRegistry.Application.Features.Menu.Commands.SaveMenuItem;
using DigitalRegistry.Application.Features.Menu.Commands.SetRecipe;
using DigitalRegistry.Application.Features.Menu.Queries.GetMenu;
using DigitalRegistry.Application.Features.Menu.Queries.GetMenuItem;
using DigitalRegistry.Application.Features.Menu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// The menu: what guests see, and what a manager maintains behind it.
/// </summary>
public class MenuController : ApiControllerBase
{
    /// <summary>Lists menu items, optionally filtered by category.</summary>
    /// <response code="200">The menu.</response>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ViewMenu)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<MenuItemDto>))]
    public async Task<ActionResult> Get(
        [FromQuery] GetMenuQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(query, cancellationToken));

    /// <summary>Returns one item with its recipe and what it costs to make. Management only.</summary>
    /// <remarks>
    /// The counterpart to the guest-facing listing above. What a dish is made of is not a guest's
    /// business and would leak supplier information, so it lives behind the management policy.
    /// </remarks>
    /// <response code="404">No menu item with that id.</response>
    [HttpGet("items/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManageMenu)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MenuItemDetailDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetItem(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetMenuItemQuery(id), cancellationToken));

    /// <summary>Creates or amends something the venue sells.</summary>
    /// <remarks>
    /// The recipe is set separately, so an item can go on the price list before anybody has worked
    /// out what goes into it. Until it has one it consumes no stock.
    /// </remarks>
    /// <response code="409">Something of that name is already on the menu.</response>
    [HttpPost("items")]
    [Authorize(Policy = AuthorizationPolicies.ManageMenu)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MenuItemDetailDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> SaveItem(
        [FromBody] SaveMenuItemCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));

    /// <summary>Replaces an item's recipe — what one serving consumes.</summary>
    /// <remarks>
    /// Send the whole recipe: an ingredient left out of the list is dropped from it. A bottled drink
    /// sold as it comes is one line consuming one unit, so the same mechanism covers bar and kitchen.
    /// </remarks>
    /// <response code="404">The item, or one of the ingredients, does not belong to this restaurant.</response>
    [HttpPut("items/{id:guid}/recipe")]
    [Authorize(Policy = AuthorizationPolicies.ManageMenu)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(MenuItemDetailDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> SetRecipe(
        Guid id,
        [FromBody] SetRecipeCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.MenuItemId)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The menu item id in the route does not match the id in the body."
            });
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }

    /// <summary>Removes a menu item.</summary>
    /// <response code="409">The item appears on past orders; withdraw it instead of deleting it.</response>
    [HttpDelete("items/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManageMenu)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> DeleteItem(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeleteMenuItemCommand(id), cancellationToken));
}
