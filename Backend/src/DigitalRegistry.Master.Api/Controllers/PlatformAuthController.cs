using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Features.Platform.Commands.PlatformLogin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Master.Api.Controllers;

/// <summary>
/// Sign-in for platform administrators.
/// </summary>
[AllowAnonymous]
[Route("api/platform/auth")]
public class PlatformAuthController : ApiControllerBase
{
    /// <summary>Exchanges email and password for a master API access token.</summary>
    /// <remarks>
    /// No restaurant code, unlike the till's sign-in: these accounts belong to no venue. A restaurant
    /// owner's credentials are refused here even when correct.
    /// </remarks>
    /// <response code="200">Authentication succeeded.</response>
    /// <response code="401">The email or password was incorrect, or the account is not an administrator.</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthenticationResult))]
    public async Task<ActionResult> Login(
        [FromBody] PlatformLoginCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));
}
