using DigitalRegistry.Application.Common.Events;
using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DigitalRegistry.Application.Features.Reservations.EventHandlers;

/// <summary>Alerts the floor that a booked party has arrived and needs seating.</summary>
internal sealed class ReservationArrivalAlertNotificationHandler(INotificationService notificationService)
    : INotificationHandler<DomainEventNotification<ReservationArrivalAlertDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<ReservationArrivalAlertDomainEvent> notification,
        CancellationToken cancellationToken) =>
        notificationService.ReservationArrivalAlertAsync(notification.DomainEvent, cancellationToken);
}

/// <summary>
/// Records a cancellation.
/// </summary>
/// <remarks>
/// No push: the freed slot shows up the next time anyone asks for availability, and interrupting the
/// floor for a booking that is no longer coming would be noise.
/// </remarks>
internal sealed class ReservationCancelledLoggingHandler(ILogger<ReservationCancelledLoggingHandler> logger)
    : INotificationHandler<DomainEventNotification<ReservationCancelledDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<ReservationCancelledDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Reservation {ReservationId} on table {TableId} was cancelled.",
            notification.DomainEvent.ReservationId,
            notification.DomainEvent.TableId);

        return Task.CompletedTask;
    }
}
