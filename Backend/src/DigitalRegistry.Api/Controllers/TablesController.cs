using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Application.Features.Tables.Commands.CreateTable;
using DigitalRegistry.Application.Features.Tables.Commands.DeleteTable;
using DigitalRegistry.Application.Features.Tables.Commands.GenerateQrCode;
using DigitalRegistry.Application.Features.Tables.Commands.InitializeTableSession;
using DigitalRegistry.Application.Features.Tables.Commands.UpdateTable;
using DigitalRegistry.Application.Features.Tables.Queries.GetAvailableTables;
using DigitalRegistry.Application.Features.Tables.Queries.GetTableById;
using DigitalRegistry.Application.Features.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Controllers;

/// <summary>
/// Floor plan management, table availability and QR table sessions.
/// </summary>
public class TablesController : ApiControllerBase
{
    /// <summary>Lists tables that can seat a party over the given period.</summary>
    /// <response code="200">The matching tables, each with its status.</response>
    [HttpGet("availability")]
    [Authorize(Policy = AuthorizationPolicies.ViewTableAvailability)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<TableAvailabilityDto>))]
    public async Task<ActionResult> GetAvailability(
        [FromQuery] GetAvailableTablesQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(query, cancellationToken));

    /// <summary>Fetches one table, including its QR token.</summary>
    /// <response code="200">The table.</response>
    /// <response code="404">No table with that id.</response>
    // Route names are global across the application, so each controller's GetById needs its own.
    [HttpGet("{id:guid}", Name = "GetTableById")]
    [Authorize(Policy = AuthorizationPolicies.ManageTables)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TableDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetTableByIdQuery(id), cancellationToken));

    /// <summary>Adds a table to the floor plan.</summary>
    /// <response code="201">The table was created.</response>
    /// <response code="409">That table number is already in use.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ManageTables)]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(TableDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Create(
        [FromBody] CreateTableCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);

        return ToCreatedResult(result, nameof(GetById), new { id = result.Succeeded ? result.Value.Id : Guid.Empty });
    }

    /// <summary>Updates a table's number, capacity or in-service flag.</summary>
    /// <response code="204">The table was updated.</response>
    /// <response code="404">No table with that id.</response>
    /// <response code="409">The number is taken, or the new capacity is below an upcoming booking.</response>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManageTables)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Update(
        Guid id,
        [FromBody] UpdateTableCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The id in the route does not match the id in the body."
            });
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }

    /// <summary>Deletes a table that has no order or reservation history.</summary>
    /// <response code="204">The table was deleted.</response>
    /// <response code="404">No table with that id.</response>
    /// <response code="409">The table has history; deactivate it instead.</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManageTables)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new DeleteTableCommand(id), cancellationToken));

    /// <summary>Rotates a table's QR token, invalidating previously printed codes.</summary>
    /// <response code="200">The new token.</response>
    /// <response code="404">No table with that id.</response>
    [HttpPost("{id:guid}/qr-code")]
    [Authorize(Policy = AuthorizationPolicies.ManageTables)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TableQrCodeDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> RotateQrCode(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GenerateTableQrCodeTokenCommand(id), cancellationToken));

    /// <summary>
    /// Exchanges a scanned QR token for a short-lived token scoped to that table.
    /// </summary>
    /// <remarks>Anonymous: a guest scans the code before they have any account.</remarks>
    /// <response code="200">A table-scoped access token.</response>
    /// <response code="404">The QR code is not valid.</response>
    [HttpPost("sessions")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthenticationResult))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> InitializeSession(
        [FromBody] InitializeTableSessionCommand command,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(command, cancellationToken));
}
