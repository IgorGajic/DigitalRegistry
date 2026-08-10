using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Commands.UpdateShift;

/// <summary>
/// Moves an existing shift's start or end. Manager and owner only.
/// </summary>
/// <remarks>
/// The waiter cannot be changed here: reassigning a shift to somebody else is a delete and a fresh
/// assignment, which keeps the record of who was put on when unambiguous.
/// </remarks>
public record UpdateShiftCommand(Guid Id, DateTime StartTime, DateTime EndTime) : IRequest<Result>;
