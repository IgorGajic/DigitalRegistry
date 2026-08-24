using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Reservations.Commands.CancelReservation;
using DigitalRegistry.Application.Features.Reservations.Commands.CheckInReservation;
using DigitalRegistry.Application.Features.Reservations.Commands.CreateReservation;
using DigitalRegistry.Application.Features.Reservations.Queries.GetDailyReservations;
using DigitalRegistry.Application.Features.Reservations.Queries.GetGuestReservations;
using DigitalRegistry.Application.Features.Reservations.Queries.GetReservationById;
using DigitalRegistry.Application.Features.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// Table bookings: making them, cancelling them, and the day's service sheet.
/// </summary>
public class ReservationsController : ApiControllerBase
{
    /// <summary>Books a table for the calling user.</summary>
    /// <response code="201">The booking was created, pending confirmation.</response>
    /// <response code="404">No table with that id.</response>
    /// <response code="409">The table is too small, out of service, or already booked.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ReserveTable)]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ReservationDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Create(
        [FromBody] CreateReservationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);

        return ToCreatedResult(
            result,
            nameof(GetById),
            new { id = result.Succeeded ? result.Value.Id : Guid.Empty });
    }

    /// <summary>Fetches one booking. Guests may fetch only their own.</summary>
    /// <response code="200">The booking.</response>
    /// <response code="404">No booking with that id is visible to the caller.</response>
    [HttpGet("{id:guid}", Name = "GetReservationById")]
    [Authorize(Policy = AuthorizationPolicies.ReserveTable)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ReservationDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetReservationByIdQuery(id), cancellationToken));

    /// <summary>Lists the calling guest's own bookings.</summary>
    /// <response code="200">The caller's bookings.</response>
    [HttpGet("mine", Name = nameof(GetMine))]
    [Authorize(Policy = AuthorizationPolicies.ReserveTable)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<ReservationDto>))]
    public async Task<ActionResult> GetMine(
        [FromQuery] bool includePast,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetGuestReservationsQuery(includePast), cancellationToken));

    /// <summary>The service sheet for a day. Staff only.</summary>
    /// <response code="200">Every booking overlapping that day.</response>
    [HttpGet("schedule")]
    [Authorize(Policy = AuthorizationPolicies.ManageReservationDesk)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<ReservationScheduleEntryDto>))]
    public async Task<ActionResult> GetSchedule(
        [FromQuery] GetDailyReservationsQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(query, cancellationToken));

    /// <summary>Cancels a booking. Guests may cancel only their own.</summary>
    /// <response code="204">The booking was cancelled.</response>
    /// <response code="403">The booking belongs to another guest.</response>
    /// <response code="404">No booking with that id.</response>
    /// <response code="409">The booking has already been completed.</response>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = AuthorizationPolicies.CancelReservation)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new CancelReservationCommand(id), cancellationToken));

    /// <summary>Records a booked party arriving. Emits a floor alert.</summary>
    /// <response code="204">The party was checked in.</response>
    /// <response code="404">No booking with that id.</response>
    /// <response code="409">The booking is cancelled or already completed.</response>
    [HttpPost("{id:guid}/check-in")]
    [Authorize(Policy = AuthorizationPolicies.ManageReservationDesk)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> CheckIn(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new CheckInReservationCommand(id), cancellationToken));
}
