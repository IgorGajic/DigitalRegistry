using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class ShiftTemplateConfiguration : IEntityTypeConfiguration<ShiftTemplate>
{
    public void Configure(EntityTypeBuilder<ShiftTemplate> builder)
    {
        builder.HasKey(template => template.Id);

        builder.Property(template => template.Name)
            .IsRequired()
            .HasMaxLength(100);

        // TimeOnly maps to SQL Server's `time`; stated so the intent survives a provider change.
        builder.Property(template => template.StartTime)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(template => template.EndTime)
            .HasColumnType("time")
            .IsRequired();

        builder.Property(template => template.IsActive)
            .HasDefaultValue(true);

        // Two shifts in one venue cannot share a name; a manager picks them by name off a list.
        builder.HasIndex(template => new { template.RestaurantId, template.Name })
            .IsUnique();

        // Both are computed from the two times and must never become columns that could contradict them.
        builder.Ignore(template => template.CrossesMidnight);
        builder.Ignore(template => template.Duration);

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_ShiftTemplate_Period", "[StartTime] <> [EndTime]"));
    }
}
