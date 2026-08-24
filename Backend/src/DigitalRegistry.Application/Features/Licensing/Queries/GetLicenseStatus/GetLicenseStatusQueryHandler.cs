using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Licensing.Queries.GetLicenseStatus;

public class GetLicenseStatusQueryHandler(
    IDigitalRegistryDbContext context,
    ILicenseService licenseService,
    ITenantContext tenant)
    : IRequestHandler<GetLicenseStatusQuery, Result<LicenseStatusDto>>
{
    /// <summary>
    /// How far ahead the renewal warning starts.
    /// </summary>
    /// <remarks>
    /// A fortnight is long enough for an owner to arrange payment without the banner becoming
    /// wallpaper they stop reading.
    /// </remarks>
    private const int ExpiryWarningDays = 14;

    public async Task<Result<LicenseStatusDto>> Handle(
        GetLicenseStatusQuery request,
        CancellationToken cancellationToken)
    {
        if (!tenant.HasTenant)
        {
            return Result<LicenseStatusDto>.Forbidden("This endpoint is only meaningful for restaurant staff.");
        }

        // Read without the filter: this runs for the caller's own restaurant, but Restaurants is not
        // restaurant-scoped, so the lookup is by id like any other foreign key.
        var restaurantName = await context.Restaurants
            .AsNoTracking()
            .Where(restaurant => restaurant.Id == tenant.RestaurantId)
            .Select(restaurant => restaurant.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (restaurantName is null)
        {
            return Result<LicenseStatusDto>.NotFound("The restaurant on this token no longer exists.");
        }

        var state = await licenseService.GetStateAsync(tenant.RestaurantId, cancellationToken);

        return Result<LicenseStatusDto>.Success(new LicenseStatusDto(
            RestaurantName: restaurantName,
            IsValid: state.IsValid,
            Status: state.Status,
            ExpiresAtUtc: state.ExpiresAtUtc,
            DaysRemaining: state.DaysRemaining,
            Plan: state.Plan,
            IsExpiringSoon: state.IsExpiringSoon(ExpiryWarningDays)));
    }
}
