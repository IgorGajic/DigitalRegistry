using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.HasKey(restaurant => restaurant.Id);

        builder.Property(restaurant => restaurant.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(restaurant => restaurant.Slug)
            .IsRequired()
            .HasMaxLength(64);

        // The slug is half of every staff member's user name, so it has to resolve to one venue.
        builder.HasIndex(restaurant => restaurant.Slug)
            .IsUnique();

        builder.Property(restaurant => restaurant.Address).HasMaxLength(300);
        builder.Property(restaurant => restaurant.ContactEmail).HasMaxLength(256);
        builder.Property(restaurant => restaurant.PhoneNumber).HasMaxLength(40);

        builder.Property(restaurant => restaurant.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        builder.Property(restaurant => restaurant.TimeZoneId)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(restaurant => restaurant.IsActive)
            .HasDefaultValue(true);
    }
}
