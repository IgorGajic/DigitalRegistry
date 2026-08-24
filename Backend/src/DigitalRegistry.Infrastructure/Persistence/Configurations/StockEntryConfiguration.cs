using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class StockEntryConfiguration : IEntityTypeConfiguration<StockEntry>
{
    public void Configure(EntityTypeBuilder<StockEntry> builder)
    {
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Quantity).HasPrecision(18, 3).IsRequired();
        builder.Property(entry => entry.PurchaseUnitPrice).HasPrecision(18, 4).IsRequired();
        builder.Property(entry => entry.TotalCost).HasPrecision(18, 2).IsRequired();

        builder.Property(entry => entry.Supplier).HasMaxLength(200);
        builder.Property(entry => entry.ReferenceNumber).HasMaxLength(100);
        builder.Property(entry => entry.Note).HasMaxLength(500);

        builder.HasOne(entry => entry.Ingredient)
            .WithMany()
            .HasForeignKey(entry => entry.IngredientId)
            // A delivery is the purchase record behind the stock and outlives the ingredient's use.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entry => entry.RecordedBy)
            .WithMany()
            .HasForeignKey(entry => entry.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entry => new { entry.RestaurantId, entry.EntryDateUtc });
        builder.HasIndex(entry => entry.IngredientId);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_StockEntry_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_StockEntry_Price_NonNegative", "[PurchaseUnitPrice] >= 0");
            table.HasCheckConstraint("CK_StockEntry_Cost_NonNegative", "[TotalCost] >= 0");
        });
    }
}
