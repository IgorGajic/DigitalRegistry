using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Menu;
using DigitalRegistry.Application.Features.Menu.Queries.GetMenu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// The menu, as seen by guests and staff alike.
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
}
