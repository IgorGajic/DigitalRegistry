using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Exceptions;
using Xunit;

namespace DigitalRegistry.Domain.UnitTests.Entities;

/// <summary>
/// The rule that decides whether a restaurant may trade.
/// </summary>
/// <remarks>
/// Every assertion passes an explicit "now", which is the point of the entity taking one: a licence's
/// whole life can be exercised without waiting a month for it.
/// </remarks>
public class LicenseTests
{
    private static readonly Guid RestaurantId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Issue_RunsForTheTermOfThePlan()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Quarterly, 12_000m, AdminId, Now);

        Assert.Equal(Now, license.StartsAtUtc);
        Assert.Equal(Now.AddMonths(3), license.ExpiresAtUtc);
        Assert.Equal(3, license.TermMonths);
        Assert.Equal(LicenseStatus.Active, license.Status);
    }

    [Theory]
    [InlineData(LicensePlan.Monthly, 1)]
    [InlineData(LicensePlan.Quarterly, 3)]
    [InlineData(LicensePlan.SemiAnnual, 6)]
    [InlineData(LicensePlan.Annual, 12)]
    public void Plan_ValueIsItsTermInMonths(LicensePlan plan, int expectedMonths)
    {
        var license = License.Issue(RestaurantId, plan, 0m, AdminId, Now);

        Assert.Equal(expectedMonths, license.TermMonths);
        Assert.Equal(Now.AddMonths(expectedMonths), license.ExpiresAtUtc);
    }

    [Fact]
    public void Issue_RejectsANegativePrice()
    {
        Assert.Throws<DomainException>(() =>
            License.Issue(RestaurantId, LicensePlan.Monthly, -1m, AdminId, Now));
    }

    [Fact]
    public void IsValidAt_IsTrueWhileTheTermRuns()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Monthly, 5_000m, AdminId, Now);

        Assert.True(license.IsValidAt(Now));
        Assert.True(license.IsValidAt(Now.AddDays(29)));
    }

    [Fact]
    public void IsValidAt_IsFalseFromTheInstantTheTermEnds()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Monthly, 5_000m, AdminId, Now);

        Assert.False(license.IsValidAt(license.ExpiresAtUtc));
        Assert.False(license.IsValidAt(license.ExpiresAtUtc.AddSeconds(1)));
    }

    [Fact]
    public void StatusAt_ReportsExpiredWithoutTheStoredValueChanging()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Monthly, 5_000m, AdminId, Now);
        var afterExpiry = license.ExpiresAtUtc.AddDays(1);

        Assert.Equal(LicenseStatus.Expired, license.StatusAt(afterExpiry));

        // Nothing has to run overnight to keep the column honest, which is the whole point.
        Assert.Equal(LicenseStatus.Active, license.Status);
    }

    [Fact]
    public void DaysRemaining_RoundsUpSoATradingVenueNeverReadsZero()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Monthly, 5_000m, AdminId, Now);

        // A few minutes left is still a day, not none.
        Assert.Equal(1, license.DaysRemainingAt(license.ExpiresAtUtc.AddMinutes(-5)));
        Assert.Equal(0, license.DaysRemainingAt(license.ExpiresAtUtc));
        Assert.Equal(0, license.DaysRemainingAt(license.ExpiresAtUtc.AddDays(10)));
    }

    [Fact]
    public void Renew_BeforeExpiryExtendsFromTheExistingEndDate()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Monthly, 5_000m, AdminId, Now);
        var originalExpiry = license.ExpiresAtUtc;

        // Paying a week early must not cost the venue that week.
        var renewedAt = originalExpiry.AddDays(-7);
        var newExpiry = license.Renew(LicensePlan.Monthly, AdminId, renewedAt);

        Assert.Equal(originalExpiry.AddMonths(1), newExpiry);
    }

    [Fact]
    public void Renew_AfterExpiryStartsFreshFromNow()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Monthly, 5_000m, AdminId, Now);

        // The intervening fortnight was not paid for, so it is not granted.
        var renewedAt = license.ExpiresAtUtc.AddDays(14);
        var newExpiry = license.Renew(LicensePlan.Monthly, AdminId, renewedAt);

        Assert.Equal(renewedAt.AddMonths(1), newExpiry);
    }

    [Fact]
    public void Renew_LetsASuspendedVenueBackIn()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Monthly, 5_000m, AdminId, Now);
        license.Suspend("Unpaid invoice.");

        Assert.False(license.IsValidAt(Now));

        license.Renew(LicensePlan.Monthly, AdminId, Now);

        Assert.Equal(LicenseStatus.Active, license.Status);
        Assert.True(license.IsValidAt(Now));
    }

    [Fact]
    public void Renew_RefusesACancelledLicence()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Monthly, 5_000m, AdminId, Now);
        license.Cancel("Contract ended.");

        Assert.Throws<DomainException>(() => license.Renew(LicensePlan.Monthly, AdminId, Now));
    }

    [Fact]
    public void Suspend_StopsAVenueTradingMidTerm()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Annual, 100_000m, AdminId, Now);

        license.Suspend("Unpaid invoice.");

        Assert.Equal(LicenseStatus.Suspended, license.Status);
        // Still inside its term, and still refused.
        Assert.False(license.IsValidAt(Now));
        Assert.Equal(LicenseStatus.Suspended, license.StatusAt(Now));
    }

    [Fact]
    public void Suspend_RequiresAReason()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Monthly, 5_000m, AdminId, Now);

        Assert.Throws<DomainException>(() => license.Suspend("   "));
    }

    [Fact]
    public void Reactivate_RestoresTheTimeTheTermHadLeft()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Annual, 100_000m, AdminId, Now);
        var expiry = license.ExpiresAtUtc;

        license.Suspend("Unpaid invoice.");
        license.Reactivate();

        Assert.True(license.IsValidAt(Now));
        Assert.Equal(expiry, license.ExpiresAtUtc);
    }

    [Fact]
    public void Reactivate_RefusesALicenceThatIsNotSuspended()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Monthly, 5_000m, AdminId, Now);

        Assert.Throws<DomainException>(license.Reactivate);
    }

    [Fact]
    public void Cancel_EndsTheLicenceEvenWithTimeLeft()
    {
        var license = License.Issue(RestaurantId, LicensePlan.Annual, 100_000m, AdminId, Now);

        license.Cancel("Contract ended.");

        Assert.Equal(LicenseStatus.Cancelled, license.Status);
        Assert.False(license.IsValidAt(Now));
    }
}
