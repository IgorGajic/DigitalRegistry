using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.HasKey(reservation => reservation.Id);

        builder.Property(reservation => reservation.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(reservation => reservation.PartySize)
            .IsRequired();

        builder.HasOne(reservation => reservation.Table)
            .WithMany(table => table.Reservations)
            .HasForeignKey(reservation => reservation.TableId)
            // Tables are deactivated rather than deleted; never cascade away booking history.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reservation => reservation.Guest)
            .WithMany(user => user.Reservations)
            .HasForeignKey(reservation => reservation.GuestId)
            .OnDelete(DeleteBehavior.Restrict);

        // The double-booking check filters by table and then by time window.
        builder.HasIndex(reservation => new { reservation.TableId, reservation.StartTime, reservation.EndTime });

        // "My reservations" and the daily service sheet.
        builder.HasIndex(reservation => new { reservation.GuestId, reservation.StartTime });
        builder.HasIndex(reservation => new { reservation.RestaurantId, reservation.StartTime });

        builder.Ignore(reservation => reservation.TimeRange);
        builder.Ignore(reservation => reservation.BlocksTable);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_Reservation_Period", "[EndTime] > [StartTime]"));
    }
}
