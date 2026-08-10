namespace DigitalRegistry.Application.Features.Shifts;

/// <summary>A shift on the schedule.</summary>
public record ShiftDto(
    Guid Id,
    Guid WaiterId,
    string WaiterName,
    DateTime StartTime,
    DateTime EndTime,
    Guid AssignedByManagerId);
