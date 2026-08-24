using DigitalRegistry.Api.Shared.Controllers;
using DigitalRegistry.Application.Features.Platform;
using DigitalRegistry.Application.Features.Platform.Commands.CreateRestaurant;
using DigitalRegistry.Application.Features.Platform.Commands.CreateRestaurantOwner;
using DigitalRegistry.Application.Features.Platform.Commands.SetRestaurantActive;
using DigitalRegistry.Application.Features.Platform.Commands.UpdateRestaurant;
using DigitalRegistry.Application.Features.Platform.Queries.GetRestaurantById;
using DigitalRegistry.Application.Features.Platform.Queries.GetRestaurants;
using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Master.Api.Controllers;

/// <summary>
/// The venues on the platform.
/// </summary>
[Authorize(Policy = PlatformAuthorization.PlatformAdminOnly)]
[Route("api/platform/restaurants")]
public class RestaurantsController : ApiControllerBase
{
    /// <summary>Lists venues with their licence standing, closest to lapsing first.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<RestaurantSummaryDto>))]
    public async Task<ActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] LicenseStatus? licenseStatus,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(
            new GetRestaurantsQuery(search, licenseStatus, isActive),
            cancellationToken));

    /// <summary>Returns one venue.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RestaurantSummaryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new GetRestaurantByIdQuery(id), cancellationToken));

    /// <summary>Registers a new venue.</summary>
    /// <remarks>
    /// The venue is created with no licence and no owner, so it cannot yet be used. Both are added by
    /// the two endpoints below.
    /// </remarks>
    /// <response code="201">The venue was registered.</response>
    /// <response code="409">The sign-in code is already taken.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(RestaurantSummaryDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Create(
        [FromBody] CreateRestaurantCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);

        return ToCreatedResult(result, nameof(GetById), new { id = result.Succeeded ? result.Value.Id : Guid.Empty });
    }

    /// <summary>Amends a venue's details. The sign-in code cannot be changed.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RestaurantSummaryDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> Update(
        Guid id,
        [FromBody] UpdateRestaurantCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return Problem(
                title: "The restaurant id in the route does not match the id in the body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }

    /// <summary>Switches a venue off entirely; its staff can no longer sign in.</summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RestaurantSummaryDto))]
    public async Task<ActionResult> Suspend(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new SetRestaurantActiveCommand(id, false), cancellationToken));

    /// <summary>Switches a venue back on. Its licence still has to be valid for the till to work.</summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RestaurantSummaryDto))]
    public async Task<ActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await Sender.Send(new SetRestaurantActiveCommand(id, true), cancellationToken));

    /// <summary>Creates the venue's owner account.</summary>
    /// <remarks>
    /// The only staff account the platform creates; managers and waiters are added by the owner from
    /// inside the till.
    /// </remarks>
    /// <response code="409">The venue already has an owner.</response>
    [HttpPost("{id:guid}/owner")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreatedUserDto))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    public async Task<ActionResult> CreateOwner(
        Guid id,
        [FromBody] CreateRestaurantOwnerCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.RestaurantId)
        {
            return Problem(
                title: "The restaurant id in the route does not match the id in the body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return ToActionResult(await Sender.Send(command, cancellationToken));
    }
}
