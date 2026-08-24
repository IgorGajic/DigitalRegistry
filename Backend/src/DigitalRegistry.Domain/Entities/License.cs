using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Exceptions;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A restaurant's paid right to use the till for a period.
/// </summary>
/// <remarks>
/// A platform-level entity: deliberately not <see cref="IRestaurantScoped"/>, because the master
/// application has to enumerate licences across every venue. It still carries a
/// <see cref="RestaurantId"/> — as an ordinary foreign key, not as a tenant discriminator.
/// <para>
/// A restaurant accumulates licences over time, one row per term bought. The one that matters is the
/// latest, and <see cref="IsValidAt"/> is the single question the till asks of it.
/// </para>
/// </remarks>
public class License : BaseEntity
{
    public Guid RestaurantId { get; set; }

    public Restaurant? Restaurant { get; set; }

    public LicensePlan Plan { get; set; }

    public DateTime StartsAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>
    /// The administrator's standing decision about this licence.
    /// </summary>
    /// <remarks>
    /// Only ever <see cref="LicenseStatus.Active"/>, <see cref="LicenseStatus.Suspended"/> or
    /// <see cref="LicenseStatus.Cancelled"/>. Expiry is not stored — see <see cref="LicenseStatus"/>.
    /// </remarks>
    public LicenseStatus Status { get; set; } = LicenseStatus.Active;

    /// <summary>What the restaurant was charged for this term, in the platform's currency.</summary>
    public decimal Price { get; set; }

    /// <summary>The platform administrator who issued or last renewed it.</summary>
    public Guid IssuedByAdminId { get; set; }

    public string? Notes { get; set; }

    public ICollection<LicensePayment> Payments { get; set; } = new List<LicensePayment>();

    /// <summary>The term's length in months, taken from the plan.</summary>
    public int TermMonths => (int)Plan;

    /// <summary>
    /// True when the till should let this restaurant work at the given moment.
    /// </summary>
    /// <remarks>
    /// Takes the current time as an argument rather than reading the clock, so the same rule can be
    /// tested at any point in a licence's life without waiting for it.
    /// </remarks>
    public bool IsValidAt(DateTime utcNow) => Status == LicenseStatus.Active && utcNow < ExpiresAtUtc;

    /// <summary>The status as it actually stands at the given moment, expiry included.</summary>
    public LicenseStatus StatusAt(DateTime utcNow) =>
        Status == LicenseStatus.Active && utcNow >= ExpiresAtUtc
            ? LicenseStatus.Expired
            : Status;

    /// <summary>
    /// Whole days left before the licence lapses; zero once it has.
    /// </summary>
    /// <remarks>
    /// Rounded up, so a licence with any time left at all reads as at least one day rather than
    /// telling a venue it has none while it is still trading.
    /// </remarks>
    public int DaysRemainingAt(DateTime utcNow) =>
        utcNow >= ExpiresAtUtc ? 0 : (int)Math.Ceiling((ExpiresAtUtc - utcNow).TotalDays);

    /// <summary>
    /// Issues a licence starting now.
    /// </summary>
    public static License Issue(
        Guid restaurantId,
        LicensePlan plan,
        decimal price,
        Guid issuedByAdminId,
        DateTime utcNow,
        string? notes = null)
    {
        if (price < 0m)
        {
            throw new DomainException("A licence price cannot be negative.");
        }

        return new License
        {
            RestaurantId = restaurantId,
            Plan = plan,
            StartsAtUtc = utcNow,
            ExpiresAtUtc = utcNow.AddMonths((int)plan),
            Status = LicenseStatus.Active,
            Price = price,
            IssuedByAdminId = issuedByAdminId,
            Notes = notes
        };
    }

    /// <summary>
    /// Extends the licence by another term.
    /// </summary>
    /// <remarks>
    /// A licence renewed before it lapses is extended from its existing end date, so a venue that pays
    /// early is not charged for the days it gives up. One renewed after it has already expired starts
    /// afresh from now, because the intervening days were not paid for.
    /// </remarks>
    /// <returns>The new expiry.</returns>
    public DateTime Renew(LicensePlan plan, Guid renewedByAdminId, DateTime utcNow)
    {
        if (Status == LicenseStatus.Cancelled)
        {
            throw new DomainException("A cancelled licence cannot be renewed; issue a new one.");
        }

        var extendFrom = ExpiresAtUtc > utcNow ? ExpiresAtUtc : utcNow;

        Plan = plan;
        ExpiresAtUtc = extendFrom.AddMonths((int)plan);
        // Renewing is also how a suspended venue is let back in once it settles up.
        Status = LicenseStatus.Active;
        IssuedByAdminId = renewedByAdminId;

        return ExpiresAtUtc;
    }

    /// <summary>Switches the licence off before its end date, without ending the contract.</summary>
    public void Suspend(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A suspension must state a reason.");
        }

        if (Status == LicenseStatus.Cancelled)
        {
            throw new DomainException("A cancelled licence cannot be suspended.");
        }

        Status = LicenseStatus.Suspended;
        Notes = reason;
    }

    /// <summary>Lifts a suspension, restoring whatever time the licence had left.</summary>
    public void Reactivate()
    {
        if (Status != LicenseStatus.Suspended)
        {
            throw new DomainException($"Only a suspended licence can be reactivated; this one is {Status}.");
        }

        Status = LicenseStatus.Active;
    }

    /// <summary>Ends the licence for good.</summary>
    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A cancellation must state a reason.");
        }

        Status = LicenseStatus.Cancelled;
        Notes = reason;
    }
}
