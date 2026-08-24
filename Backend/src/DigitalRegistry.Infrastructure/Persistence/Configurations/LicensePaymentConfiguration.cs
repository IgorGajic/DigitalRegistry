using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class LicensePaymentConfiguration : IEntityTypeConfiguration<LicensePayment>
{
    public void Configure(EntityTypeBuilder<LicensePayment> builder)
    {
        builder.HasKey(payment => payment.Id);

        builder.Property(payment => payment.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(payment => payment.PaymentMethod)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(payment => payment.ReferenceNumber)
            .HasMaxLength(100);

        builder.Property(payment => payment.Notes)
            .HasMaxLength(500);

        builder.HasOne(payment => payment.License)
            .WithMany(license => license.Payments)
            .HasForeignKey(payment => payment.LicenseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Revenue on the master dashboard is grouped by month.
        builder.HasIndex(payment => payment.PaidAtUtc);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_LicensePayment_Amount_NonNegative", "[Amount] >= 0"));
    }
}
