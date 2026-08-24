using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Licensing;

/// <summary>
/// The licence position shown to a restaurant's own staff.
/// </summary>
/// <remarks>
/// Deliberately says nothing about price or payments: what a venue was charged is between the owner
/// and the platform, and the till has no reason to display it to whoever is on shift.
/// </remarks>
/// <param name="RestaurantName">For the banner, so the owner can see which venue is meant.</param>
/// <param name="IsValid">Whether the till is usable right now.</param>
/// <param name="Status">The licence's standing, expiry included.</param>
/// <param name="ExpiresAtUtc">End of the current term, or null when none was ever issued.</param>
/// <param name="DaysRemaining">Whole days left, rounded up; zero once lapsed.</param>
/// <param name="Plan">Length of the current term.</param>
/// <param name="IsExpiringSoon">True when the renewal warning should be shown.</param>
public record LicenseStatusDto(
    string RestaurantName,
    bool IsValid,
    LicenseStatus Status,
    DateTime? ExpiresAtUtc,
    int DaysRemaining,
    LicensePlan? Plan,
    bool IsExpiringSoon);
