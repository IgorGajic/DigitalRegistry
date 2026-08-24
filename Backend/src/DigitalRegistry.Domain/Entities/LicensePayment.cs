using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// Money received from a restaurant for a licence.
/// </summary>
/// <remarks>
/// Entered by hand by a platform administrator: the system records that a payment arrived, it does not
/// take one. There is no payment provider behind this, so nothing here should be read as a guarantee
/// that funds actually cleared.
/// <para>
/// Several rows may point at one licence — a term paid in instalments, or a correction — which is why
/// this is a collection on <see cref="License"/> rather than a single amount on it.
/// </para>
/// </remarks>
public class LicensePayment : BaseEntity
{
    public Guid LicenseId { get; set; }

    public License? License { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaidAtUtc { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>Bank reference, invoice number or similar, for reconciliation.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>The platform administrator who entered the payment.</summary>
    public Guid RecordedByAdminId { get; set; }

    public string? Notes { get; set; }
}
