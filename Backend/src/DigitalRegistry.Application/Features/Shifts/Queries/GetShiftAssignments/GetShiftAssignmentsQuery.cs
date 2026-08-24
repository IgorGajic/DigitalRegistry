using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Queries.GetShiftAssignments;

/// <summary>The standing rota: who works which shift on which days.</summary>
/// <param name="WaiterId">Narrows to one waiter.</param>
/// <param name="OnDate">
/// Narrows to arrangements in force on a date, so a manager can ask what the rota currently is rather
/// than seeing every arrangement ever made.
/// </param>
public record GetShiftAssignmentsQuery(Guid? WaiterId = null, DateOnly? OnDate = null)
    : IRequest<Result<IReadOnlyList<ShiftAssignmentDto>>>;
