using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reservations.Commands.CreateReservation;

/// <summary>
/// Books a table for the calling user.
/// </summary>
/// <remarks>
/// There is no guest id on the command: the booking is always made for the authenticated caller,
/// taken from their token, so nobody can create bookings in someone else's name.
/// </remarks>
public record CreateReservationCommand(
    Guid TableId,
    DateTime StartTime,
    DateTime EndTime,
    int PartySize) : IRequest<Result<ReservationDto>>;
