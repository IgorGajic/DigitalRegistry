using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Commands.SetRestaurantActive;

/// <summary>
/// Switches a venue on or off outright.
/// </summary>
/// <remarks>
/// Separate from suspending a licence. Suspension is a commercial lever within a paid term; this is
/// the contract ending. A deactivated venue cannot even sign in, so its staff never reach the licence
/// screen — which is the intended difference.
/// </remarks>
public record SetRestaurantActiveCommand(Guid Id, bool IsActive) : IRequest<Result<RestaurantSummaryDto>>;
