using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(room => room.Id);

        builder.Property(room => room.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasMany(room => room.Tables)
            .WithOne(table => table.Room)
            .HasForeignKey(table => table.RoomId)
            // Deleting a room leaves its tables in place, unplaced. They may carry order history, and
            // rearranging the floor should never be a way to lose it.
            .OnDelete(DeleteBehavior.SetNull);

        // Two rooms in one venue cannot share a name; different venues are free to both have a "Sala".
        builder.HasIndex(room => new { room.RestaurantId, room.Name })
            .IsUnique();

        // The floor screen lists a restaurant's rooms in tab order.
        builder.HasIndex(room => new { room.RestaurantId, room.DisplayOrder });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Room_Canvas_Positive", "[CanvasWidth] > 0 AND [CanvasHeight] > 0");
        });
    }
}
