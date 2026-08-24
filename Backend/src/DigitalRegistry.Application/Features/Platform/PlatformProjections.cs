using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform;

/// <summary>
/// Shared reads for the master application.
/// </summary>
/// <remarks>
/// Every query here calls <c>IgnoreQueryFilters()</c>, which is the one place in the system where
/// seeing across restaurants is intended. Keeping those calls together means the exception to the
/// tenant rule is auditable in a single file rather than scattered through handlers.
/// <para>
/// Licence state is computed here from the stored rows rather than through <c>ILicenseService</c>:
/// that service answers for one restaurant and caches, which is the wrong shape for a list of every
/// venue on the platform.
/// </para>
/// </remarks>
internal static class PlatformProjections
{
    /// <summary>How near expiry a licence has to be to appear on the dashboard's warning list.</summary>
    public const int ExpiringSoonDays = 30;

    /// <summary>Every restaurant, tenant filters bypassed.</summary>
    public static IQueryable<Restaurant> AllRestaurants(this IDigitalRegistryDbContext context) =>
        context.Restaurants.IgnoreQueryFilters();

    /// <summary>Every licence, tenant filters bypassed.</summary>
    public static IQueryable<License> AllLicenses(this IDigitalRegistryDbContext context) =>
        context.Licenses.IgnoreQueryFilters();

    /// <summary>Every licence payment, tenant filters bypassed.</summary>
    public static IQueryable<LicensePayment> AllLicensePayments(this IDigitalRegistryDbContext context) =>
        context.LicensePayments.IgnoreQueryFilters();

    /// <summary>
    /// Builds the summary the master application lists venues by.
    /// </summary>
    /// <remarks>
    /// The governing licence is the one running latest, matching what the till enforces. Loaded per
    /// restaurant rather than joined so the "latest by expiry" rule stays stated once.
    /// </remarks>
    public static async Task<List<RestaurantSummaryDto>> SummariseAsync(
        this IDigitalRegistryDbContext context,
        IQueryable<Restaurant> restaurants,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var rows = await restaurants
            .AsNoTracking()
            .Select(restaurant => new
            {
                restaurant.Id,
                restaurant.Name,
                restaurant.Slug,
                restaurant.ContactEmail,
                restaurant.PhoneNumber,
                restaurant.Address,
                restaurant.CurrencyCode,
                restaurant.TimeZoneId,
                restaurant.IsActive,
                restaurant.Created,
                License = context.Licenses
                    .IgnoreQueryFilters()
                    .Where(license => license.RestaurantId == restaurant.Id)
                    .OrderByDescending(license => license.ExpiresAtUtc)
                    .FirstOrDefault(),
                StaffCount = context.Users.Count(user =>
                    user.RestaurantId == restaurant.Id && user.Role != UserRole.Guest)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new RestaurantSummaryDto(
                Id: row.Id,
                Name: row.Name,
                Slug: row.Slug,
                ContactEmail: row.ContactEmail,
                PhoneNumber: row.PhoneNumber,
                Address: row.Address,
                CurrencyCode: row.CurrencyCode,
                TimeZoneId: row.TimeZoneId,
                IsActive: row.IsActive,
                Created: row.Created,
                LicenseStatus: row.License?.StatusAt(utcNow) ?? LicenseStatus.Expired,
                LicenseExpiresAtUtc: row.License?.ExpiresAtUtc,
                DaysRemaining: row.License?.DaysRemainingAt(utcNow) ?? 0,
                Plan: row.License?.Plan,
                StaffCount: row.StaffCount))
            .ToList();
    }

    /// <summary>Projects a licence together with what has been paid against it.</summary>
    public static async Task<List<LicenseDto>> ToDtosAsync(
        this IQueryable<License> licenses,
        IDigitalRegistryDbContext context,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var rows = await licenses
            .AsNoTracking()
            .Select(license => new
            {
                License = license,
                RestaurantName = context.Restaurants
                    .IgnoreQueryFilters()
                    .Where(restaurant => restaurant.Id == license.RestaurantId)
                    .Select(restaurant => restaurant.Name)
                    .FirstOrDefault(),
                AmountPaid = context.LicensePayments
                    .IgnoreQueryFilters()
                    .Where(payment => payment.LicenseId == license.Id)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new LicenseDto(
                Id: row.License.Id,
                RestaurantId: row.License.RestaurantId,
                RestaurantName: row.RestaurantName ?? string.Empty,
                Plan: row.License.Plan,
                TermMonths: row.License.TermMonths,
                StartsAtUtc: row.License.StartsAtUtc,
                ExpiresAtUtc: row.License.ExpiresAtUtc,
                Status: row.License.StatusAt(utcNow),
                DaysRemaining: row.License.DaysRemainingAt(utcNow),
                Price: row.License.Price,
                AmountPaid: row.AmountPaid,
                Notes: row.License.Notes))
            .ToList();
    }
}
