using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Features.Auth.Commands.Login;
using DigitalRegistry.Application.Features.Auth.Commands.RegisterGuest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// Account registration and sign-in. Open to everyone, per the access matrix.
/// </summary>
[AllowAnonymous]
public class AuthController : ApiControllerBase
{
    /// <summary>Registers a new guest account at one restaurant and returns an access token.</summary>
    /// <response code="200">The account was created and is signed in.</response>
    /// <response code="404">No restaurant is registered under that code.</response>
    /// <response code="409">An account already exists for that email at that restaurant.</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthenticationResult))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Register(
        [FromBody] RegisterGuestCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));

    /// <summary>Exchanges a restaurant code, email and password for an access token.</summary>
    /// <remarks>
    /// The restaurant code is required because an email address identifies an account only within one
    /// venue. The token that comes back confines every subsequent request to that restaurant.
    /// </remarks>
    /// <response code="200">Authentication succeeded.</response>
    /// <response code="401">The restaurant code, email or password was incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthenticationResult))]
    public async Task<ActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));
}
