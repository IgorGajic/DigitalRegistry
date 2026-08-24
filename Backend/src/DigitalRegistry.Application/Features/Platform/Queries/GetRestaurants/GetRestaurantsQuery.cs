using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Queries.GetRestaurants;

/// <summary>
/// Lists venues on the platform.
/// </summary>
/// <param name="Search">Matches on name or sign-in code.</param>
/// <param name="LicenseStatus">
/// Filters by licence standing as at now, expiry included — so filtering by
/// <see cref="Domain.Enums.LicenseStatus.Expired"/> finds venues whose term has simply run out, which
/// is never a stored value.
/// </param>
/// <param name="IsActive">Filters on whether the venue itself is switched on.</param>
public record GetRestaurantsQuery(
    string? Search = null,
    LicenseStatus? LicenseStatus = null,
    bool? IsActive = null) : IRequest<Result<IReadOnlyList<RestaurantSummaryDto>>>;
