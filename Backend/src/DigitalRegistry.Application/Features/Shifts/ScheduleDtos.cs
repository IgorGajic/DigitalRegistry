using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Shifts;

/// <summary>A named working period the venue runs, in the venue's own local time.</summary>
public record ShiftTemplateDto(
    Guid Id,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool CrossesMidnight,
    double DurationHours,
    bool IsActive,
    int AssignmentCount);

/// <summary>A standing arrangement of one waiter to one shift on given days.</summary>
public record ShiftAssignmentDto(
    Guid Id,
    Guid WaiterId,
    string WaiterName,
    Guid ShiftTemplateId,
    string ShiftTemplateName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    WeekDays Days,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

/// <summary>What generating a rota over a period did.</summary>
/// <param name="Created">Shifts written.</param>
/// <param name="AlreadyPresent">
/// Shifts the arrangements called for that were already on the schedule. Generating the same weeks
/// twice tops up what is missing rather than duplicating what is there.
/// </param>
/// <param name="Conflicts">
/// Shifts not written because the waiter was already booked over that period by something else — a
/// second arrangement, or a cover shift entered by hand.
/// </param>
public record GenerateScheduleResultDto(
    DateOnly FromDate,
    DateOnly ToDate,
    int Created,
    int AlreadyPresent,
    IReadOnlyList<ScheduleConflictDto> Conflicts);

/// <summary>One shift the generator declined to write, and why.</summary>
public record ScheduleConflictDto(
    DateOnly Date,
    Guid WaiterId,
    string WaiterName,
    string ShiftTemplateName,
    DateTime StartUtc,
    DateTime EndUtc,
    string Reason);

/// <summary>The rota for one week, as the grid a manager reads.</summary>
/// <param name="Days">The seven dates the columns stand for.</param>
public record WeeklyScheduleDto(
    DateOnly WeekStart,
    IReadOnlyList<DateOnly> Days,
    IReadOnlyList<WaiterWeekDto> Waiters);

/// <summary>One waiter's row in the weekly grid.</summary>
/// <param name="TotalHours">What the week comes to, for the manager watching the wage bill.</param>
public record WaiterWeekDto(
    Guid WaiterId,
    string WaiterName,
    double TotalHours,
    IReadOnlyList<ScheduledShiftDto> Shifts);

/// <summary>One shift in the weekly grid.</summary>
/// <param name="Date">The local day the shift belongs to, which is the column it sits in.</param>
/// <param name="IsGenerated">
/// False for a shift entered by hand — a swap or a cover — so the manager can see what departs from
/// the standing rota.
/// </param>
public record ScheduledShiftDto(
    Guid Id,
    DateOnly Date,
    DateTime StartUtc,
    DateTime EndUtc,
    double Hours,
    string? ShiftTemplateName,
    bool IsGenerated);
