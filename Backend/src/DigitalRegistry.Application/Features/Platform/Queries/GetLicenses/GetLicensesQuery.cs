using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Queries.GetLicenses;

/// <summary>
/// Lists licence terms across the platform.
/// </summary>
/// <param name="RestaurantId">Narrows to one venue's licence history.</param>
/// <param name="Status">Filters by standing as at now, expiry included.</param>
public record GetLicensesQuery(
    Guid? RestaurantId = null,
    LicenseStatus? Status = null) : IRequest<Result<IReadOnlyList<LicenseDto>>>;
