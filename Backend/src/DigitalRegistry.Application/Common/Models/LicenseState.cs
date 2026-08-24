using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Common.Models;

/// <summary>
/// What the till needs to know about a restaurant's licence, as at the moment it was asked.
/// </summary>
/// <param name="IsValid">Whether the restaurant may use the till right now.</param>
/// <param name="Status">The licence's standing, expiry included.</param>
/// <param name="ExpiresAtUtc">When the current term ends, or null when no licence was ever issued.</param>
/// <param name="DaysRemaining">Whole days left, rounded up; zero once lapsed.</param>
/// <param name="Plan">The current term's length, or null when no licence was ever issued.</param>
public record LicenseState(
    bool IsValid,
    LicenseStatus Status,
    DateTime? ExpiresAtUtc,
    int DaysRemaining,
    LicensePlan? Plan)
{
    /// <summary>
    /// The state of a restaurant that has never had a licence, or has been deactivated outright.
    /// </summary>
    /// <remarks>
    /// Reported as expired rather than as a separate "never licensed" case: from the till's point of
    /// view the two are the same refusal, and giving them one shape keeps the guard simple.
    /// </remarks>
    public static LicenseState None { get; } =
        new(IsValid: false, LicenseStatus.Expired, ExpiresAtUtc: null, DaysRemaining: 0, Plan: null);

    /// <summary>
    /// True when the term is close enough to its end to warn the owner about.
    /// </summary>
    public bool IsExpiringSoon(int withinDays) => IsValid && DaysRemaining <= withinDays;
}
