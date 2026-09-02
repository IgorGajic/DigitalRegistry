using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Settings;

/// <summary>
/// How a venue's till presents itself.
/// </summary>
/// <remarks>
/// Read by every member of staff, not just the owner: the theme decides which colours the floor
/// screen is drawn in, so a waiter needs it as much as the person who chose it.
/// </remarks>
public record RestaurantSettingsDto(string RestaurantName, AppTheme Theme);
