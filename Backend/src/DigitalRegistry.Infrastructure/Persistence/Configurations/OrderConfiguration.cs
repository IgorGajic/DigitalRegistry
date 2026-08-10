using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(order => order.Id);

        builder.Property(order => order.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(order => order.Table)
            .WithMany(table => table.Orders)
            .HasForeignKey(order => order.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(order => order.Waiter)
            .WithMany()
            .HasForeignKey(order => order.WaiterId)
            // Null for guest QR self-orders, so the relationship is optional.
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(order => order.OrderItems)
            .WithOne(orderItem => orderItem.Order)
            .HasForeignKey(orderItem => orderItem.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Finding the open tab for a table is the single most frequent order query.
        builder.HasIndex(order => new { order.TableId, order.Status });
        builder.HasIndex(order => order.CreatedAt);

        builder.Ignore(order => order.Total);
        builder.Ignore(order => order.PlacedByGuest);
        builder.Ignore(order => order.IsEditable);
        builder.Ignore(order => order.IsClosed);
    }
}
