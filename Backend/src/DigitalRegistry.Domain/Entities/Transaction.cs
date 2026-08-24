using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// The immutable record of a payment taken against an order.
/// </summary>
public class Transaction : BaseEntity, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProcessedByWaiterId { get; set; }

    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The payment this one backs out, when it is a reversal rather than a payment.
    /// </summary>
    /// <remarks>
    /// A reversal is a second row carrying a negative <see cref="Amount"/>, not an edit to the
    /// original. Financial history is written once: summing the column still gives the true takings,
    /// and the fact that a bill was reversed remains visible rather than being erased.
    /// </remarks>
    public Guid? ReversesTransactionId { get; set; }

    public Transaction? Reverses { get; set; }

    public bool IsReversal => ReversesTransactionId is not null;

    public Order? Order { get; set; }

    public ApplicationUser? ProcessedByWaiter { get; set; }
}
