using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.HasKey(ingredient => ingredient.Id);

        builder.Property(ingredient => ingredient.Name)
            .HasMaxLength(200)
            .IsRequired();

        // Three decimals so recipes can express fractions of a gram or millilitre.
        builder.Property(ingredient => ingredient.StockQuantity)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(ingredient => ingredient.LowStockThreshold)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.Property(ingredient => ingredient.AveragePurchasePrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(ingredient => ingredient.Unit)
            .HasConversion<int>()
            .IsRequired();

        // Unique within a restaurant, not across the platform: every venue keeps its own store.
        builder.HasIndex(ingredient => new { ingredient.RestaurantId, ingredient.Name }).IsUnique();

        builder.Ignore(ingredient => ingredient.IsLowOnStock);
        builder.Ignore(ingredient => ingredient.StockValue);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Ingredient_Stock_NonNegative", "[StockQuantity] >= 0");
            table.HasCheckConstraint("CK_Ingredient_Threshold_NonNegative", "[LowStockThreshold] >= 0");
            table.HasCheckConstraint("CK_Ingredient_AvgPrice_NonNegative", "[AveragePurchasePrice] >= 0");
        });
    }
}
