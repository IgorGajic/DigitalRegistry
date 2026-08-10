using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reservations.Queries.GetDailyReservations;

/// <summary>
/// The service sheet for one day: every booking, in start order.
/// </summary>
/// <param name="Date">The day to list, interpreted in UTC. Defaults to today when omitted.</param>
/// <param name="TableId">Optionally narrows the sheet to a single table.</param>
public record GetDailyReservationsQuery(DateOnly? Date = null, Guid? TableId = null)
    : IRequest<Result<IReadOnlyList<ReservationScheduleEntryDto>>>;
