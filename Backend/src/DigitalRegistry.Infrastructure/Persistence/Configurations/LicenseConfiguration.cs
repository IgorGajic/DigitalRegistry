using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class LicenseConfiguration : IEntityTypeConfiguration<License>
{
    public void Configure(EntityTypeBuilder<License> builder)
    {
        builder.HasKey(license => license.Id);

        builder.Property(license => license.Plan)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(license => license.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(license => license.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(license => license.Notes)
            .HasMaxLength(500);

        builder.HasOne(license => license.Restaurant)
            .WithMany(restaurant => restaurant.Licenses)
            .HasForeignKey(license => license.RestaurantId)
            // Licences are the commercial record of the relationship and outlive the venue's use of
            // the product.
            .OnDelete(DeleteBehavior.Restrict);

        // The licence that governs is the one running latest for a restaurant, which is exactly this
        // index read backwards — the query the till makes on every request.
        builder.HasIndex(license => new { license.RestaurantId, license.ExpiresAtUtc });

        // The master dashboard lists what is about to lapse across every venue.
        builder.HasIndex(license => new { license.Status, license.ExpiresAtUtc });

        builder.Ignore(license => license.TermMonths);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_License_Price_NonNegative", "[Price] >= 0");
            table.HasCheckConstraint("CK_License_Period", "[ExpiresAtUtc] > [StartsAtUtc]");
        });
    }
}
