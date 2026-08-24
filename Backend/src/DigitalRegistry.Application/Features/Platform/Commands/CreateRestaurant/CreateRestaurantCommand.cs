using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Commands.CreateRestaurant;

/// <summary>
/// Registers a new venue on the platform.
/// </summary>
/// <remarks>
/// Creates the tenant only. It is issued no licence and given no owner account here, so a
/// half-finished registration cannot accidentally leave a venue able to trade; both are separate,
/// deliberate steps.
/// </remarks>
/// <param name="Slug">
/// The code staff will type at sign-in. Immutable once set, because it forms part of every user name
/// at the venue — see <see cref="Common.Security.TenantUserName"/>.
/// </param>
public record CreateRestaurantCommand(
    string Name,
    string Slug,
    string? Address,
    string? ContactEmail,
    string? PhoneNumber,
    string? CurrencyCode,
    string? TimeZoneId) : IRequest<Result<RestaurantSummaryDto>>;
