using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.HasKey(menuItem => menuItem.Id);

        builder.Property(menuItem => menuItem.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(menuItem => menuItem.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(menuItem => menuItem.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(menuItem => menuItem.IsAvailable)
            .HasDefaultValue(true);

        builder.HasMany(menuItem => menuItem.Recipe)
            .WithOne(recipeItem => recipeItem.MenuItem)
            .HasForeignKey(recipeItem => recipeItem.MenuItemId)
            // The recipe has no meaning without its menu item.
            .OnDelete(DeleteBehavior.Cascade);

        // The guest-facing menu is grouped by category and hides unavailable items. Every read is
        // already narrowed to one restaurant by the global query filter, so that column leads.
        builder.HasIndex(menuItem => new { menuItem.RestaurantId, menuItem.Category, menuItem.IsAvailable });

        // Names need only be unique within a restaurant — two venues may both sell an "Espresso".
        builder.HasIndex(menuItem => new { menuItem.RestaurantId, menuItem.Name }).IsUnique();

        builder.Ignore(menuItem => menuItem.Price);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_MenuItem_UnitPrice_NonNegative", "[UnitPrice] >= 0"));
    }
}
