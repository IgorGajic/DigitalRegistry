using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Settings;
using DigitalRegistry.Application.Features.Settings.Commands.UpdateRestaurantTheme;
using DigitalRegistry.Application.Features.Settings.Queries.GetRestaurantSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// How the venue's till presents itself.
/// </summary>
/// <remarks>
/// The two halves are guarded differently on purpose. Reading is open to every signed-in member of
/// staff, because the theme decides which colours the floor screen is drawn in and a waiter needs
/// that as much as the owner does; writing is the owner's, because it is a decision made once for
/// the whole venue.
/// <para>
/// Reading also answers a QR table session, which is authenticated as a guest. Left that way after
/// checking what it discloses: the venue's own name, which the guest is looking at on their own
/// screen, and which colour it is painted in. Nothing commercial, unlike the licence endpoint next
/// door — and it is what would let the guest screen wear the venue's palette.
/// </para>
/// </remarks>
[Authorize]
public class SettingsController : ApiControllerBase
{
    /// <summary>Returns the caller's venue and the palette it is painted in.</summary>
    /// <response code="200">The venue's settings.</response>
    /// <response code="404">The restaurant on the token no longer exists.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RestaurantSettingsDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Get(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetRestaurantSettingsQuery(), cancellationToken));

    /// <summary>Repaints the venue's till.</summary>
    /// <response code="200">The settings as they now stand.</response>
    /// <response code="400">No such theme.</response>
    /// <response code="403">The caller is not the owner.</response>
    [HttpPut("theme")]
    [Authorize(Policy = AuthorizationPolicies.ManageRestaurantSettings)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RestaurantSettingsDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> UpdateTheme(
        [FromBody] UpdateRestaurantThemeCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));
}
