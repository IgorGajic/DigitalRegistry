using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasKey(movement => movement.Id);

        builder.Property(movement => movement.Type).HasConversion<int>().IsRequired();

        // Signed, so no non-negative constraint here — a sale is a negative quantity by design.
        builder.Property(movement => movement.Quantity).HasPrecision(18, 3).IsRequired();
        builder.Property(movement => movement.BalanceAfter).HasPrecision(18, 3).IsRequired();

        builder.Property(movement => movement.Note).HasMaxLength(500);

        builder.HasOne(movement => movement.Ingredient)
            .WithMany()
            .HasForeignKey(movement => movement.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.Order)
            .WithMany()
            .HasForeignKey(movement => movement.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(movement => movement.StockEntry)
            .WithMany()
            .HasForeignKey(movement => movement.StockEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        // The consumption report reads a period and groups by ingredient and type.
        builder.HasIndex(movement => new { movement.RestaurantId, movement.OccurredAtUtc });
        builder.HasIndex(movement => new { movement.IngredientId, movement.OccurredAtUtc });

        // A movement always has a quantity; a zero one would be a ledger line saying nothing happened.
        builder.ToTable(table =>
            table.HasCheckConstraint("CK_StockMovement_Quantity_NonZero", "[Quantity] <> 0"));
    }
}
