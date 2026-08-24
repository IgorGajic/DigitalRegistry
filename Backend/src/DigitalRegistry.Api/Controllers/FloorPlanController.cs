using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.FloorPlan;
using DigitalRegistry.Application.Features.FloorPlan.Commands.CreateRoom;
using DigitalRegistry.Application.Features.FloorPlan.Commands.DeleteRoom;
using DigitalRegistry.Application.Features.FloorPlan.Commands.SaveRoomLayout;
using DigitalRegistry.Application.Features.FloorPlan.Commands.UpdateRoom;
using DigitalRegistry.Application.Features.FloorPlan.Queries.GetFloorPlan;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// The drawn floor: rooms, where the tables stand, and what is running on each.
/// </summary>
/// <remarks>
/// Reading is open to anyone who may see table availability — this is the till's main screen and every
/// waiter lives on it. Rearranging is behind <see cref="AuthorizationPolicies.ManageTables"/>, the same
/// policy that governs creating and removing tables.
/// </remarks>
[Route("api/floor-plan")]
public class FloorPlanController : ApiControllerBase
{
    /// <summary>Returns every room with its tables and their current status.</summary>
    /// <param name="includeInactive">
    /// Include tables taken out of service. Off for the floor screen; on for the layout editor, which
    /// has to be able to see and move them.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ViewTableAvailability)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(FloorPlanDto))]
    public async Task<ActionResult> Get(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        ToActionResult(await Sender.Send(new GetFloorPlanQuery(includeInactive), cancellationToken));

    /// <summary>Adds a room.</summary>
    /// <response code="409">A room of that name already exists.</response>
    [HttpPost("rooms")]
    [Authorize(Policy = AuthorizationPolicies.ManageTables)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RoomDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> CreateRoom(
        [FromBody] CreateRoomCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));

    /// <summary>Renames a room, reorders its tab, or resizes its drawing area.</summary>
    /// <response code="409">The name is taken, or shrinking would strand a table outside the room.</response>
    [HttpPut("rooms/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManageTables)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RoomDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> UpdateRoom(
        Guid id,
        [FromBody] UpdateRoomCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                title: "The room id in the route does not match the id in the body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }

    /// <summary>Removes a room. Its tables survive, unplaced.</summary>
    /// <response code="409">The room still has open tabs.</response>
    [HttpDelete("rooms/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManageTables)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> DeleteRoom(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeleteRoomCommand(id), cancellationToken));

    /// <summary>
    /// Stores the arrangement of one room in a single write.
    /// </summary>
    /// <remarks>
    /// Send the room's whole layout, not individual moves. Tables listed are placed in the room;
    /// tables currently in it but omitted are taken out, which is how the editor removes one by
    /// dragging it away.
    /// </remarks>
    /// <response code="400">A table would fall outside the room's area.</response>
    /// <response code="404">The room, or one of the tables, does not belong to this restaurant.</response>
    [HttpPut("rooms/{id:guid}/layout")]
    [Authorize(Policy = AuthorizationPolicies.ManageTables)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RoomDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> SaveLayout(
        Guid id,
        [FromBody] SaveRoomLayoutCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.RoomId)
        {
            return Problem(
                title: "The room id in the route does not match the id in the body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }
}
