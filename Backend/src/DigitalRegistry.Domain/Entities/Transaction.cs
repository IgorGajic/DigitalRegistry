using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// The immutable record of a payment taken against an order.
/// </summary>
public class Transaction : BaseEntity
{
    public Guid OrderId { get; set; }

    public Guid ProcessedByWaiterId { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    public Order? Order { get; set; }

    public ApplicationUser? ProcessedByWaiter { get; set; }
}
