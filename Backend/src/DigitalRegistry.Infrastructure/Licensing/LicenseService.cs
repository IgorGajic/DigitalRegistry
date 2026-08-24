using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Infrastructure.Licensing;

/// <summary>
/// Resolves a restaurant's licence state from the database.
/// </summary>
/// <remarks>
/// Deliberately uncached — see <see cref="ILicenseService"/> for why. Both reads below are covered by
/// indexes and return at most one small row, which is what makes answering per request affordable.
/// </remarks>
internal sealed class LicenseService(
    IDigitalRegistryDbContext dbContext,
    IDateTimeService dateTime) : ILicenseService
{
    public async Task<LicenseState> GetStateAsync(
        Guid restaurantId,
        CancellationToken cancellationToken = default)
    {
        if (restaurantId == Guid.Empty)
        {
            return LicenseState.None;
        }

        var utcNow = dateTime.UtcNow;

        // A venue switched off outright by the platform is refused whatever its licence says.
        var isRestaurantActive = await dbContext.Restaurants
            .AsNoTracking()
            .Where(restaurant => restaurant.Id == restaurantId)
            .Select(restaurant => (bool?)restaurant.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (isRestaurantActive is not true)
        {
            return LicenseState.None;
        }

        // A restaurant accumulates one licence row per term bought; the one that governs is the one
        // running latest, which is also the one a renewal extends. Served by the
        // (RestaurantId, ExpiresAtUtc) index read backwards.
        var license = await dbContext.Licenses
            .AsNoTracking()
            .Where(candidate => candidate.RestaurantId == restaurantId)
            .OrderByDescending(candidate => candidate.ExpiresAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (license is null)
        {
            return LicenseState.None;
        }

        return new LicenseState(
            IsValid: license.IsValidAt(utcNow),
            Status: license.StatusAt(utcNow),
            ExpiresAtUtc: license.ExpiresAtUtc,
            DaysRemaining: license.DaysRemainingAt(utcNow),
            Plan: license.Plan);
    }
}
