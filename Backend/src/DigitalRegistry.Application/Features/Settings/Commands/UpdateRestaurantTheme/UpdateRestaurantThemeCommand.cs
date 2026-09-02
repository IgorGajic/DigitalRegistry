using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Settings.Commands.UpdateRestaurantTheme;

/// <summary>Repaints the venue's till.</summary>
/// <remarks>
/// Carries no restaurant id. The venue is the one on the caller's token, which is what keeps an
/// owner from repainting somebody else's.
/// </remarks>
public record UpdateRestaurantThemeCommand(AppTheme Theme) : IRequest<Result<RestaurantSettingsDto>>;
