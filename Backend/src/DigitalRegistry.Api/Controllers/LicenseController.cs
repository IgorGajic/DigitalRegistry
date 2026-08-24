using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Features.Licensing.Queries.GetLicenseStatus;
using DigitalRegistry.Application.Features.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// The restaurant's own licence position.
/// </summary>
/// <remarks>
/// Exempt from the licence guard by design — a venue that cannot use the till still has to be able to
/// find out why. Authentication is still required: the answer is about the caller's restaurant, which
/// is read from their token.
/// </remarks>
[Authorize]
public class LicenseController : ApiControllerBase
{
    /// <summary>Returns whether the till is licensed, and for how much longer.</summary>
    /// <response code="200">The licence position.</response>
    /// <response code="404">The restaurant on the token no longer exists.</response>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LicenseStatusDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetStatus(CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetLicenseStatusQuery(), cancellationToken));
}
