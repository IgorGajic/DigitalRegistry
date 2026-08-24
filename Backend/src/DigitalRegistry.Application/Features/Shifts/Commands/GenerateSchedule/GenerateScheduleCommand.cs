using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Commands.GenerateSchedule;

/// <summary>
/// Turns the standing arrangements into actual shifts over a period.
/// </summary>
/// <remarks>
/// Safe to run repeatedly over the same weeks: a shift the arrangements call for that is already on
/// the schedule is left alone rather than written twice. That is what lets a manager extend the rota
/// by another month without first working out where the last run stopped.
/// <para>
/// Anything the generator cannot write because the waiter is already booked is reported back rather
/// than skipped silently — a clash is something the manager has to resolve, not something to discover
/// from an empty schedule.
/// </para>
/// </remarks>
/// <param name="WaiterId">Narrows the run to one waiter.</param>
public record GenerateScheduleCommand(
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? WaiterId = null) : IRequest<Result<GenerateScheduleResultDto>>;
