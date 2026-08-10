using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.HasKey(shift => shift.Id);

        builder.HasOne(shift => shift.Waiter)
            .WithMany(user => user.Shifts)
            .HasForeignKey(shift => shift.WaiterId)
            .OnDelete(DeleteBehavior.Cascade);

        // The overlap check reads every shift for one waiter around a time window.
        builder.HasIndex(shift => new { shift.WaiterId, shift.StartTime, shift.EndTime });

        // The schedule view is queried by date range across all waiters.
        builder.HasIndex(shift => shift.StartTime);

        builder.Ignore(shift => shift.TimeRange);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_Shift_Period", "[EndTime] > [StartTime]"));
    }
}
