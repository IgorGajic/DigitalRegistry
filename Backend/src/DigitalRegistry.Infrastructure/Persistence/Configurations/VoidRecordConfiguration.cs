using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class VoidRecordConfiguration : IEntityTypeConfiguration<VoidRecord>
{
    public void Configure(EntityTypeBuilder<VoidRecord> builder)
    {
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(record => record.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(record => record.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(record => record.ItemName)
            .HasMaxLength(200);

        builder.HasOne(record => record.Order)
            .WithMany()
            .HasForeignKey(record => record.OrderId)
            // The trail outlives everything it describes.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(record => record.MenuItem)
            .WithMany()
            .HasForeignKey(record => record.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(record => record.PerformedBy)
            .WithMany()
            .HasForeignKey(record => record.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(record => record.ApprovedBy)
            .WithMany()
            .HasForeignKey(record => record.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The report reads by period, and then narrows to one member of staff.
        builder.HasIndex(record => new { record.RestaurantId, record.VoidedAtUtc });
        builder.HasIndex(record => new { record.RestaurantId, record.PerformedByUserId, record.VoidedAtUtc });

        builder.HasIndex(record => record.OrderId);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_VoidRecord_Amount_NonNegative", "[Amount] >= 0");
            table.HasCheckConstraint("CK_VoidRecord_Quantity_NonNegative", "[Quantity] >= 0");
        });
    }
}
