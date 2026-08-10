using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Commands.DeleteShift;

/// <summary>Takes a waiter off a shift. Manager and owner only.</summary>
public record DeleteShiftCommand(Guid Id) : IRequest<Result>;
