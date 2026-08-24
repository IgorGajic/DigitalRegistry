using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(user => user.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(user => user.Restaurant)
            .WithMany()
            .HasForeignKey(user => user.RestaurantId)
            // Refuse to delete a restaurant that still has accounts; the master application
            // deactivates a venue rather than erasing who worked there.
            .OnDelete(DeleteBehavior.Restrict);

        // Staff listings filter by restaurant and role together. Null RestaurantId — the platform
        // administrators — sits at one end of the index and stays cheap to find.
        builder.HasIndex(user => new { user.RestaurantId, user.Role });

        builder.Ignore(user => user.FullName);
    }
}
