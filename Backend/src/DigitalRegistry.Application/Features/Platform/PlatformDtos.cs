using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Platform;

/// <summary>
/// A venue as the platform administrator sees it: the record plus where its licence stands.
/// </summary>
public record RestaurantSummaryDto(
    Guid Id,
    string Name,
    string Slug,
    string? ContactEmail,
    string? PhoneNumber,
    string? Address,
    string CurrencyCode,
    string TimeZoneId,
    bool IsActive,
    DateTime Created,
    LicenseStatus LicenseStatus,
    DateTime? LicenseExpiresAtUtc,
    int DaysRemaining,
    LicensePlan? Plan,
    int StaffCount);

/// <summary>One licence term, with what has been paid against it.</summary>
public record LicenseDto(
    Guid Id,
    Guid RestaurantId,
    string RestaurantName,
    LicensePlan Plan,
    int TermMonths,
    DateTime StartsAtUtc,
    DateTime ExpiresAtUtc,
    LicenseStatus Status,
    int DaysRemaining,
    decimal Price,
    decimal AmountPaid,
    string? Notes);

/// <summary>A payment received against a licence.</summary>
public record LicensePaymentDto(
    Guid Id,
    Guid LicenseId,
    decimal Amount,
    DateTime PaidAtUtc,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Notes);

/// <summary>The owner account created alongside a new restaurant.</summary>
/// <param name="UserName">
/// The composite Identity user name. Shown because it is not the plain email, and an administrator
/// telling an owner how to sign in needs to know that the restaurant code is part of it.
/// </param>
public record CreatedUserDto(Guid Id, string Email, string UserName, string FullName, UserRole Role);

/// <summary>Headline figures for the master dashboard.</summary>
/// <param name="ExpiringSoon">Licences valid today but lapsing inside the warning window.</param>
/// <param name="MonthlyLicenseRevenue">Licence payments received, grouped by calendar month.</param>
public record PlatformDashboardDto(
    int TotalRestaurants,
    int ActiveRestaurants,
    int ActiveLicenses,
    int ExpiredLicenses,
    int SuspendedLicenses,
    int ExpiringSoon,
    decimal TotalLicenseRevenue,
    IReadOnlyList<MonthlyRevenueDto> MonthlyLicenseRevenue,
    IReadOnlyList<RestaurantSummaryDto> ExpiringRestaurants);

public record MonthlyRevenueDto(int Year, int Month, decimal Amount, int PaymentCount);
