using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(transaction => transaction.PaymentMethod)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(transaction => transaction.Order)
            .WithMany()
            .HasForeignKey(transaction => transaction.OrderId)
            // A payment record is financial history and outlives everything around it.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transaction => transaction.ProcessedByWaiter)
            .WithMany()
            .HasForeignKey(transaction => transaction.ProcessedByWaiterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(transaction => transaction.Reverses)
            .WithMany()
            .HasForeignKey(transaction => transaction.ReversesTransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(transaction => transaction.IsReversal);

        // One payment per order; re-paying a settled order is a conflict, not a second row. Filtered
        // to exclude reversals, which are deliberately a second row against the same order — without
        // the filter, voiding a paid bill would violate this index. Left platform-wide rather than
        // composed with the restaurant: an order id already belongs to exactly one venue.
        builder.HasIndex(transaction => transaction.OrderId)
            .IsUnique()
            .HasFilter("[ReversesTransactionId] IS NULL");

        // A reversal backs out exactly one payment, and only once.
        builder.HasIndex(transaction => transaction.ReversesTransactionId)
            .IsUnique()
            .HasFilter("[ReversesTransactionId] IS NOT NULL");

        // Financial reporting reads by date, and per-waiter cash-up reads by waiter and date. Both
        // always run inside one restaurant, so that column leads.
        builder.HasIndex(transaction => new { transaction.RestaurantId, transaction.TransactionDate });
        builder.HasIndex(transaction => new
        {
            transaction.RestaurantId,
            transaction.ProcessedByWaiterId,
            transaction.TransactionDate
        });

        builder.ToTable(table =>
            // A payment takes money in and a reversal gives it back, so the sign follows which one
            // the row is. Constraining both directions — rather than dropping the check to allow
            // negatives — keeps a payment from ever being stored as a negative amount.
            table.HasCheckConstraint(
                "CK_Transaction_Amount_Sign",
                "([ReversesTransactionId] IS NULL AND [Amount] >= 0) "
                + "OR ([ReversesTransactionId] IS NOT NULL AND [Amount] <= 0)"));
    }
}
