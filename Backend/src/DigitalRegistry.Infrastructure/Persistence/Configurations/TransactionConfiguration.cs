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

        // One payment per order; re-paying a settled order is a conflict, not a second row.
        builder.HasIndex(transaction => transaction.OrderId).IsUnique();

        // Financial reporting reads by date, and per-waiter cash-up reads by waiter and date.
        builder.HasIndex(transaction => transaction.TransactionDate);
        builder.HasIndex(transaction => new { transaction.ProcessedByWaiterId, transaction.TransactionDate });

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_Transaction_Amount_NonNegative", "[Amount] >= 0"));
    }
}
