using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reservations.Commands.CancelReservation;

/// <summary>
/// Cancels a booking, freeing the table's time slot.
/// </summary>
/// <remarks>
/// A guest may cancel only their own booking; a manager or owner may cancel any. Waiters are
/// excluded by the access matrix.
/// </remarks>
public record CancelReservationCommand(Guid Id) : IRequest<Result>;
