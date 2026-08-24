using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Commands.DeleteShiftAssignment;

/// <summary>
/// Cancels a standing arrangement.
/// </summary>
/// <remarks>
/// Shifts already generated from it stay on the schedule — people worked them, or are expecting to.
/// Ending an arrangement stops future rotas being built from it, and the manager clears any unwanted
/// shifts that were already written individually.
/// </remarks>
public record DeleteShiftAssignmentCommand(Guid Id) : IRequest<Result>;
