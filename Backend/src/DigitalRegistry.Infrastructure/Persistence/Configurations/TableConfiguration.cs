using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class TableConfiguration : IEntityTypeConfiguration<Table>
{
    public void Configure(EntityTypeBuilder<Table> builder)
    {
        builder.HasKey(table => table.Id);

        // Two tables in the same restaurant cannot share a floor number; different restaurants are
        // free to both have a table 1.
        builder.HasIndex(table => new { table.RestaurantId, table.TableNumber })
            .IsUnique();

        // A QR token is the sole credential a guest presents, and it is resolved before any tenant is
        // known, so it must be unique platform-wide rather than per restaurant.
        builder.HasIndex(table => table.QrCodeToken)
            .IsUnique();

        builder.Property(table => table.Capacity)
            .IsRequired();

        builder.Property(table => table.IsActive)
            .HasDefaultValue(true);

        builder.Property(table => table.Shape)
            .HasConversion<int>()
            .IsRequired();

        // The floor screen loads one room at a time.
        builder.HasIndex(table => table.RoomId);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Table_Capacity_Positive", "[Capacity] > 0");
            table.HasCheckConstraint("CK_Table_Number_Positive", "[TableNumber] > 0");
            table.HasCheckConstraint("CK_Table_Size_Positive", "[Width] > 0 AND [Height] > 0");
            table.HasCheckConstraint("CK_Table_Rotation_Range", "[Rotation] >= 0 AND [Rotation] < 360");
        });
    }
}
