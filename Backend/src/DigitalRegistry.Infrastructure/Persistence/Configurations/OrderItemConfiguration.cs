using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(orderItem => orderItem.Id);

        builder.Property(orderItem => orderItem.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(orderItem => orderItem.Quantity)
            .IsRequired();

        builder.Property(orderItem => orderItem.Notes)
            .HasMaxLength(500);

        builder.HasOne(orderItem => orderItem.MenuItem)
            .WithMany()
            .HasForeignKey(orderItem => orderItem.MenuItemId)
            // Order history must survive a menu item being retired.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(orderItem => orderItem.OrderId);

        builder.Ignore(orderItem => orderItem.LineTotal);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_OrderItem_Quantity_Positive", "[Quantity] > 0"));
    }
}
