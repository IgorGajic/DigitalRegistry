using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Shifts.Commands.AssignShift;
using DigitalRegistry.Application.Features.Shifts.Commands.DeleteShift;
using DigitalRegistry.Application.Features.Shifts.Commands.DeleteShiftAssignment;
using DigitalRegistry.Application.Features.Shifts.Commands.GenerateSchedule;
using DigitalRegistry.Application.Features.Shifts.Commands.SaveShiftAssignment;
using DigitalRegistry.Application.Features.Shifts.Commands.SaveShiftTemplate;
using DigitalRegistry.Application.Features.Shifts.Commands.UpdateShift;
using DigitalRegistry.Application.Features.Shifts.Queries.GetShiftAssignments;
using DigitalRegistry.Application.Features.Shifts.Queries.GetShiftTemplates;
using DigitalRegistry.Application.Features.Shifts.Queries.GetWaitersSchedule;
using DigitalRegistry.Application.Features.Shifts.Queries.GetWeeklySchedule;
using DigitalRegistry.Application.Features.Shifts;
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

    // -----------------------------------------------------------------------------------------
    // The standing rota: named shifts, who works them, and turning that into actual shifts.
    // -----------------------------------------------------------------------------------------

    /// <summary>The venue's named working periods, in the venue's own local time.</summary>
    [HttpGet("templates")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<ShiftTemplateDto>))]
    public async Task<ActionResult> GetTemplates(
        [FromQuery] bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetShiftTemplatesQuery(includeRetired), cancellationToken));

    /// <summary>Creates or amends a named working period, such as "II smena 15:00–23:00".</summary>
    /// <remarks>
    /// A shift ending at or before it starts runs past midnight; 22:00–06:00 needs no flag to say so.
    /// </remarks>
    /// <response code="409">A shift of that name already exists.</response>
    [HttpPost("templates")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ShiftTemplateDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> SaveTemplate(
        [FromBody] SaveShiftTemplateCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));

    /// <summary>The standing rota: who works which shift on which days.</summary>
    [HttpGet("assignments")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<ShiftAssignmentDto>))]
    public async Task<ActionResult> GetAssignments(
        [FromQuery] Guid? waiterId,
        [FromQuery] DateOnly? onDate,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetShiftAssignmentsQuery(waiterId, onDate), cancellationToken));

    /// <summary>Puts a waiter on a shift for given days over a given period.</summary>
    /// <remarks>
    /// Records the arrangement only; no shifts appear until the schedule is generated. A waiter
    /// already assigned to a clashing shift on any of those days is refused here rather than weeks
    /// later during generation.
    /// </remarks>
    /// <response code="409">The waiter already works a clashing shift on one of those days.</response>
    [HttpPost("assignments")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ShiftAssignmentDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> SaveAssignment(
        [FromBody] SaveShiftAssignmentCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));

    /// <summary>Cancels a standing arrangement. Shifts already generated from it stay.</summary>
    [HttpDelete("assignments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> DeleteAssignment(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeleteShiftAssignmentCommand(id), cancellationToken));

    /// <summary>Turns the standing arrangements into actual shifts over a period.</summary>
    /// <remarks>
    /// Safe to run repeatedly over the same weeks: shifts already on the schedule are left alone
    /// rather than duplicated. Anything that could not be written because the waiter was already
    /// booked comes back in the response.
    /// </remarks>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GenerateScheduleResultDto))]
    public async Task<ActionResult> Generate(
        [FromBody] GenerateScheduleCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));

    /// <summary>The rota for one week, as the grid of waiters against days.</summary>
    /// <param name="date">Any date in the week wanted; it is snapped back to the Monday.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet("week")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WeeklyScheduleDto))]
    public async Task<ActionResult> GetWeek(
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetWeeklyScheduleQuery(date), cancellationToken));
}
