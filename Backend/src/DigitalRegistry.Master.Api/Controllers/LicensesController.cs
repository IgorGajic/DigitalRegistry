using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Features.Platform;
using DigitalRegistry.Application.Features.Platform.Commands.ChangeLicenseStatus;
using DigitalRegistry.Application.Features.Platform.Commands.IssueLicense;
using DigitalRegistry.Application.Features.Platform.Commands.RecordLicensePayment;
using DigitalRegistry.Application.Features.Platform.Commands.RenewLicense;
using DigitalRegistry.Application.Features.Platform.Queries.GetLicensePayments;
using DigitalRegistry.Application.Features.Platform.Queries.GetLicenses;
using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Master.Api.Controllers;

/// <summary>
/// Licence terms and the payments made against them.
/// </summary>
[Authorize(Policy = PlatformAuthorization.PlatformAdminOnly)]
[Route("api/platform/licenses")]
public class LicensesController : ApiControllerBase
{
    /// <summary>Lists licence terms, newest expiry first.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<LicenseDto>))]
    public async Task<ActionResult> GetAll(
        [FromQuery] Guid? restaurantId,
        [FromQuery] LicenseStatus? status,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetLicensesQuery(restaurantId, status), cancellationToken));

    /// <summary>Issues a venue's first licence term.</summary>
    /// <response code="409">The venue already holds a licence; renew it instead.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LicenseDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Issue(
        [FromBody] IssueLicenseCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));

    /// <summary>Extends a licence by another term.</summary>
    /// <remarks>
    /// A term renewed before it lapses is extended from its existing end date, so paying early costs
    /// the venue nothing. This is also how a suspended venue is let back in.
    /// </remarks>
    [HttpPost("{id:guid}/renew")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LicenseDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Renew(
        Guid id,
        [FromBody] RenewLicenseCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.LicenseId)
        {
            return Problem(
                title: "The licence id in the route does not match the id in the body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }

    /// <summary>Suspends a licence mid-term. The venue's till stops working within minutes.</summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LicenseDto))]
    public async Task<ActionResult> Suspend(
        Guid id,
        [FromBody] LicenseReasonRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new ChangeLicenseStatusCommand(id, LicenseAction.Suspend, request.Reason),
            cancellationToken));

    /// <summary>Lifts a suspension, restoring whatever time the term had left.</summary>
    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LicenseDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Reactivate(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new ChangeLicenseStatusCommand(id, LicenseAction.Reactivate, string.Empty),
            cancellationToken));

    /// <summary>Ends a licence for good. A cancelled licence cannot be renewed.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LicenseDto))]
    public async Task<ActionResult> Cancel(
        Guid id,
        [FromBody] LicenseReasonRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new ChangeLicenseStatusCommand(id, LicenseAction.Cancel, request.Reason),
            cancellationToken));

    /// <summary>Lists payments recorded against a licence.</summary>
    [HttpGet("{id:guid}/payments")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<LicensePaymentDto>))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetPayments(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetLicensePaymentsQuery(id), cancellationToken));

    /// <summary>Records money received against a licence.</summary>
    /// <remarks>
    /// Bookkeeping only: it does not extend the term or restore a lapsed venue. Renewal does that.
    /// </remarks>
    [HttpPost("{id:guid}/payments")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LicensePaymentDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> RecordPayment(
        Guid id,
        [FromBody] RecordLicensePaymentCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.LicenseId)
        {
            return Problem(
                title: "The licence id in the route does not match the id in the body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }
}

/// <summary>
/// The justification an administrator must give when suspending or cancelling a licence.
/// </summary>
/// <remarks>
/// A body rather than a query string: the reason is stored against the venue and read back later, so
/// it should not end up in web-server logs alongside the URL.
/// </remarks>
public record LicenseReasonRequest(string Reason);
