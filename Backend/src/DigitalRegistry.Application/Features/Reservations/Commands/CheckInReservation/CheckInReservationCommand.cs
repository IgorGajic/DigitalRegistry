using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reservations.Commands.CheckInReservation;

/// <summary>
/// Records a booked party arriving, completing the reservation and alerting the floor.
/// </summary>
/// <remarks>
/// Not listed in the implementation roadmap, but required by the real-time specification, which calls
/// for a <c>ReservationArrivalAlert</c> to be emitted "when a guest checks in" — there has to be an
/// operation that does the checking in.
/// </remarks>
public record CheckInReservationCommand(Guid Id) : IRequest<Result>;
