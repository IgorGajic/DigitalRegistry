using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reservations.Queries.GetGuestReservations;

/// <summary>
/// Lists the calling guest's own bookings.
/// </summary>
/// <param name="IncludePast">
/// When false (the default) only bookings that have not yet ended are returned.
/// </param>
public record GetGuestReservationsQuery(bool IncludePast = false)
    : IRequest<Result<IReadOnlyList<ReservationDto>>>;
