using DigitalRegistry.Domain.Entities;

namespace DigitalRegistry.Application.Features.Shifts;

/// <summary>Projections shared by the schedule handlers.</summary>
internal static class ScheduleMapping
{
    public static ShiftTemplateDto ToDto(this ShiftTemplate template, int assignmentCount) => new(
        Id: template.Id,
        Name: template.Name,
        StartTime: template.StartTime,
        EndTime: template.EndTime,
        CrossesMidnight: template.CrossesMidnight,
        DurationHours: template.Duration.TotalHours,
        IsActive: template.IsActive,
        AssignmentCount: assignmentCount);
}
