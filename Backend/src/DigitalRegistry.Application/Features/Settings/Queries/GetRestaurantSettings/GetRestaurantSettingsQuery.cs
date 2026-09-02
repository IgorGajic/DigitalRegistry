using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Settings.Queries.GetRestaurantSettings;

/// <summary>What the signed-in member of staff's venue looks like.</summary>
public record GetRestaurantSettingsQuery : IRequest<Result<RestaurantSettingsDto>>;
