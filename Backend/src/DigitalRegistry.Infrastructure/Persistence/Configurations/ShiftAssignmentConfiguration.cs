using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> builder)
    {
        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Days)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(assignment => assignment.ValidFrom)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(assignment => assignment.ValidTo)
            .HasColumnType("date");

        builder.HasOne(assignment => assignment.Waiter)
            .WithMany()
            .HasForeignKey(assignment => assignment.WaiterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(assignment => assignment.ShiftTemplate)
            .WithMany(template => template.Assignments)
            .HasForeignKey(assignment => assignment.ShiftTemplateId)
            // Retiring a template is the supported way to withdraw it; deleting one that arrangements
            // still point at would leave a rota nobody could explain.
            .OnDelete(DeleteBehavior.Restrict);

        // The generator reads every arrangement whose period touches the range being built.
        builder.HasIndex(assignment => new { assignment.RestaurantId, assignment.ValidFrom, assignment.ValidTo });
        builder.HasIndex(assignment => assignment.WaiterId);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_ShiftAssignment_Period",
                "[ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]"));
    }
}
