using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class TableConfiguration : IEntityTypeConfiguration<Table>
{
    public void Configure(EntityTypeBuilder<Table> builder)
    {
        builder.HasKey(table => table.Id);

        // Two tables cannot share a floor number.
        builder.HasIndex(table => table.TableNumber)
            .IsUnique();

        // A QR token is the sole credential a guest presents, so it must resolve to one table.
        builder.HasIndex(table => table.QrCodeToken)
            .IsUnique();

        builder.Property(table => table.Capacity)
            .IsRequired();

        builder.Property(table => table.IsActive)
            .HasDefaultValue(true);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Table_Capacity_Positive", "[Capacity] > 0");
            table.HasCheckConstraint("CK_Table_Number_Positive", "[TableNumber] > 0");
        });
    }
}
