using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// The record that something was cancelled, by whom, and why.
/// </summary>
/// <remarks>
/// Cancelling is the point in a till where money most easily goes missing: a drink rung up, served,
/// then voided leaves stock gone and no sale against it. The control is not preventing voids — staff
/// need them — but making every one attributable. Nothing in the system may reduce what a guest is
/// charged without writing one of these.
/// <para>
/// Written once and never amended. Corrections are further records, not edits, so the trail cannot be
/// tidied up after the fact.
/// </para>
/// </remarks>
public class VoidRecord : BaseEntity, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public Guid OrderId { get; set; }

    public Order? Order { get; set; }

    public VoidType Type { get; set; }

    /// <summary>
    /// The menu item cancelled, for a <see cref="VoidType.Item"/> void; null for a whole order.
    /// </summary>
    public Guid? MenuItemId { get; set; }

    public MenuItem? MenuItem { get; set; }

    /// <summary>
    /// The item's name as it stood when cancelled.
    /// </summary>
    /// <remarks>
    /// Copied rather than joined, for the same reason <see cref="OrderItem.UnitPrice"/> is: renaming a
    /// menu item years later must not rewrite what the report says was voided. It also keeps the line
    /// readable once the order line itself has been deleted.
    /// </remarks>
    public string? ItemName { get; set; }

    /// <summary>Servings cancelled. Zero for a whole-order void, where the lines carry the detail.</summary>
    public int Quantity { get; set; }

    /// <summary>What the cancellation took off the bill, at the prices the order captured.</summary>
    public decimal Amount { get; set; }

    /// <summary>Why. Required — an unexplained void is exactly what this record exists to prevent.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>The member of staff who carried it out.</summary>
    public Guid PerformedByUserId { get; set; }

    public ApplicationUser? PerformedBy { get; set; }

    /// <summary>
    /// The manager or owner who authorised it, where authorisation was needed.
    /// </summary>
    /// <remarks>
    /// Set only for <see cref="VoidType.PaidOrder"/>. For the other two the waiter acts alone, and the
    /// record exists so the owner can review afterwards rather than approve in advance — stopping to
    /// find a manager over a mis-keyed coffee would not survive a busy service.
    /// </remarks>
    public Guid? ApprovedByUserId { get; set; }

    public ApplicationUser? ApprovedBy { get; set; }

    public DateTime VoidedAtUtc { get; set; } = DateTime.UtcNow;
}
