using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Features.Platform;
using DigitalRegistry.Application.Features.Platform.Queries.GetPlatformDashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Master.Api.Controllers;

/// <summary>
/// Headline figures across the platform.
/// </summary>
[Authorize(Policy = PlatformAuthorization.PlatformAdminOnly)]
[Route("api/platform/dashboard")]
public class DashboardController : ApiControllerBase
{
    /// <summary>
    /// Returns licence counts, licence revenue by month, and the venues closest to lapsing.
    /// </summary>
    /// <param name="revenueMonths">How many months of revenue to chart. Defaults to a year.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PlatformDashboardDto))]
    public async Task<ActionResult> Get(
        [FromQuery] int revenueMonths = 12,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetPlatformDashboardQuery(revenueMonths), cancellationToken));
}
