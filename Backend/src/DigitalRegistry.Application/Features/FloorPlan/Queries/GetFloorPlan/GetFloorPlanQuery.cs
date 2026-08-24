using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.FloorPlan.Queries.GetFloorPlan;

/// <summary>
/// The till's main screen: every room, every table, and what is running on each.
/// </summary>
/// <remarks>
/// Takes no restaurant parameter — the venue comes from the caller's token. Status is derived from
/// open tabs and reservations at the moment of asking, never read from a stored column, so it cannot
/// drift out of step with the orders it describes.
/// </remarks>
/// <param name="IncludeInactive">
/// Include tables taken out of service. Off for the floor screen, on for the layout editor, which has
/// to be able to see and move them.
/// </param>
public record GetFloorPlanQuery(bool IncludeInactive = false) : IRequest<Result<FloorPlanDto>>;
