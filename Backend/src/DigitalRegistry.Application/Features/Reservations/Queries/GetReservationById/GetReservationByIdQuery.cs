using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reservations.Queries.GetReservationById;

/// <summary>
/// Fetches one booking. A guest may fetch only their own; staff may fetch any.
/// </summary>
public record GetReservationByIdQuery(Guid Id) : IRequest<Result<ReservationDto>>;
