using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Commands.UpdateRestaurant;

/// <summary>
/// Amends a venue's details.
/// </summary>
/// <remarks>
/// The slug is absent on purpose. It is baked into every user name at the venue, so changing it would
/// silently lock out every member of staff; a venue that needs a different code needs a migration, not
/// an edit.
/// </remarks>
public record UpdateRestaurantCommand(
    Guid Id,
    string Name,
    string? Address,
    string? ContactEmail,
    string? PhoneNumber,
    string? CurrencyCode,
    string? TimeZoneId) : IRequest<Result<RestaurantSummaryDto>>;
