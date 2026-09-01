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

        // Written by the desk for a telephone booking; absent when the guest booked it themselves,
        // where the name comes from the account instead.
        builder.Property(reservation => reservation.ContactName)
            .HasMaxLength(200);

        builder.Property(reservation => reservation.ContactPhone)
            .HasMaxLength(50);

        builder.HasOne(reservation => reservation.Table)
            .WithMany(table => table.Reservations)
            .HasForeignKey(reservation => reservation.TableId)
            // Tables are deactivated rather than deleted; never cascade away booking history.
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reservation => reservation.Guest)
            .WithMany(user => user.Reservations)
            .HasForeignKey(reservation => reservation.GuestId)
            .OnDelete(DeleteBehavior.Restrict);

        // Who answered the telephone. No inverse collection: nothing asks a member of staff for the
        // bookings they took, and the audit trail is read from the reservation's own side.
        builder.HasOne(reservation => reservation.TakenBy)
            .WithMany()
            .HasForeignKey(reservation => reservation.TakenByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The double-booking check filters by table and then by time window.
        builder.HasIndex(reservation => new { reservation.TableId, reservation.StartTime, reservation.EndTime });

        // "My reservations" and the daily service sheet.
        builder.HasIndex(reservation => new { reservation.GuestId, reservation.StartTime });
        builder.HasIndex(reservation => new { reservation.RestaurantId, reservation.StartTime });

        builder.Ignore(reservation => reservation.TimeRange);
        builder.Ignore(reservation => reservation.BlocksTable);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Reservation_Period", "[EndTime] > [StartTime]");

            // A booking is either a guest's own or the desk's, and a desk booking has to say who it
            // is for. Enforced here because the service sheet has no other name to print.
            table.HasCheckConstraint(
                "CK_Reservation_Booker",
                "([GuestId] IS NOT NULL) OR ([ContactName] IS NOT NULL)");
        });
    }
}
