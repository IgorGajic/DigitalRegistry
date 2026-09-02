using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class RoomFixtureConfiguration : IEntityTypeConfiguration<RoomFixture>
{
    public void Configure(EntityTypeBuilder<RoomFixture> builder)
    {
        builder.HasKey(fixture => fixture.Id);

        builder.Property(fixture => fixture.Label)
            .IsRequired()
            .HasMaxLength(RoomFixture.MaxLabelLength);

        builder.HasOne(fixture => fixture.Room)
            .WithMany(room => room.Fixtures)
            .HasForeignKey(fixture => fixture.RoomId)
            // The opposite of what a room does to its tables, and deliberately so. A table outlives
            // its room because it carries order history; a landmark drawn in a room that no longer
            // exists is nothing at all, so it goes with it.
            .OnDelete(DeleteBehavior.Cascade);

        // The floor screen and the editor both read a room's fixtures in drawing order.
        builder.HasIndex(fixture => new { fixture.RoomId, fixture.DisplayOrder });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_RoomFixture_Size_Positive",
                $"[Width] >= {RoomFixture.MinSize} AND [Height] >= {RoomFixture.MinSize}");
        });
    }
}
