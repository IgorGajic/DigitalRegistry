using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Commands.AssignShift;

/// <summary>
/// Puts a waiter on shift. Manager and owner only.
/// </summary>
/// <remarks>
/// The assigning manager is taken from the caller's token rather than the body, so the schedule
/// always records who actually made the assignment.
/// </remarks>
public record AssignShiftCommand(Guid WaiterId, DateTime StartTime, DateTime EndTime)
    : IRequest<Result<ShiftDto>>;
