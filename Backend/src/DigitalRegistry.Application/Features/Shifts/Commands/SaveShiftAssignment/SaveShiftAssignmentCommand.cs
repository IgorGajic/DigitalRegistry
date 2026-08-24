using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Commands.SaveShiftAssignment;

/// <summary>
/// Puts a waiter on a shift for given days over a given period — the standing rota.
/// </summary>
/// <remarks>
/// Records the arrangement only. No shifts appear on the schedule until it is generated, which is
/// deliberate: a manager sketching next quarter's rota should not be writing hundreds of rows they may
/// still change their mind about.
/// </remarks>
/// <param name="ValidTo">Null for an arrangement that runs until it is cancelled.</param>
public record SaveShiftAssignmentCommand(
    Guid? Id,
    Guid WaiterId,
    Guid ShiftTemplateId,
    WeekDays Days,
    DateOnly ValidFrom,
    DateOnly? ValidTo) : IRequest<Result<ShiftAssignmentDto>>;
