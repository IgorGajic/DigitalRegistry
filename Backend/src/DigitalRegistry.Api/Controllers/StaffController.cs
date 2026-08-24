using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Staff.Commands.CreateStaffAccount;
using DigitalRegistry.Application.Features.Staff.Commands.ResetStaffPassword;
using DigitalRegistry.Application.Features.Staff.Commands.SetStaffEnabled;
using DigitalRegistry.Application.Features.Staff.Commands.UpdateStaffAccount;
using DigitalRegistry.Application.Features.Staff.Queries.GetStaff;
using DigitalRegistry.Application.Features.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// The people who work at the venue. Owner only.
/// </summary>
/// <remarks>
/// The owner's own account is created by the platform when the restaurant is registered; everybody
/// else is created here. Accounts are switched off rather than deleted, because a name has to stay
/// on every order and shift it is attached to.
/// </remarks>
[Authorize(Policy = AuthorizationPolicies.ManageStaff)]
public class StaffController : ApiControllerBase
{
    /// <summary>Lists the venue's staff. Guests are not staff and are excluded.</summary>
    /// <param name="includeDisabled">Include accounts that have been switched off.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<StaffMemberDto>))]
    public async Task<ActionResult> GetAll(
        [FromQuery] bool includeDisabled = false,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetStaffQuery(includeDisabled), cancellationToken));

    /// <summary>Takes somebody on as a waiter or a manager.</summary>
    /// <response code="409">Somebody already uses that email at this restaurant.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StaffMemberDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Create(
        [FromBody] CreateStaffAccountCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));

    /// <summary>Corrects a name, or moves somebody between waiter and manager.</summary>
    /// <remarks>The email cannot be changed: it is half of how the person signs in.</remarks>
    /// <response code="409">An attempt to change the role of the owner.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(StaffMemberDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Update(
        Guid id,
        [FromBody] UpdateStaffAccountCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                title: "The staff id in the route does not match the id in the body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }

    /// <summary>Switches an account off. The person keeps their name on all past work.</summary>
    /// <response code="409">An attempt to switch off the owner, or your own account.</response>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Disable(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new SetStaffEnabledCommand(id, false), cancellationToken));

    /// <summary>Switches an account back on.</summary>
    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Enable(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new SetStaffEnabledCommand(id, true), cancellationToken));

    /// <summary>Sets a new password for somebody who has forgotten theirs.</summary>
    /// <remarks>
    /// Takes no current password, because the point is that nobody has it. There is no self-service
    /// reset: a till has no email delivery behind it, and the owner is standing in the same room.
    /// </remarks>
    [HttpPost("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new ResetStaffPasswordCommand(id, request.NewPassword),
            cancellationToken));
}

/// <summary>The new password, in a body rather than a query string so it stays out of server logs.</summary>
public record ResetPasswordRequest(string NewPassword);
