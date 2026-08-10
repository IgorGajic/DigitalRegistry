using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Shifts;
using DigitalRegistry.Application.Features.Shifts.Commands.AssignShift;
using DigitalRegistry.Application.Features.Shifts.Commands.DeleteShift;
using DigitalRegistry.Application.Features.Shifts.Commands.UpdateShift;
using DigitalRegistry.Application.Features.Shifts.Queries.GetWaitersSchedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// The waiter roster. Manager and owner only.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.ManageShifts)]
public class ShiftsController : ApiControllerBase
{
    /// <summary>The roster over a window, optionally for one waiter.</summary>
    /// <response code="200">The matching shifts, earliest first.</response>
    [HttpGet(Name = nameof(GetSchedule))]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<ShiftDto>))]
    public async Task<ActionResult> GetSchedule(
        [FromQuery] GetWaitersScheduleQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(query, cancellationToken));

    /// <summary>Puts a waiter on shift.</summary>
    /// <response code="201">The shift was assigned.</response>
    /// <response code="400">
    /// The period is invalid, the user is not a waiter, or the shift overlaps one they already have.
    /// </response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ShiftDto))]
    public async Task<ActionResult> Assign(
        [FromBody] AssignShiftCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);

        return ToCreatedResult(result, nameof(GetSchedule), new { });
    }

    /// <summary>Moves an existing shift's start or end.</summary>
    /// <response code="204">The shift was updated.</response>
    /// <response code="400">The period is invalid or overlaps another of the waiter's shifts.</response>
    /// <response code="404">No shift with that id.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Update(
        Guid id,
        [FromBody] UpdateShiftCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The shift id in the route does not match the id in the body."
            });
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }

    /// <summary>Takes a waiter off a shift.</summary>
    /// <response code="204">The shift was removed.</response>
    /// <response code="404">No shift with that id.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeleteShiftCommand(id), cancellationToken));
}
