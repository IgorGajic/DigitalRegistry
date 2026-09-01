using DigitalRegistry.Domain.Common;

namespace DigitalRegistry.Domain.Events;

/// <summary>Raised when a guest checks in against a reservation, alerting the floor.</summary>
/// <param name="GuestId">
/// The account the booking belongs to, or null for one the desk took by telephone — the usual case.
/// </param>
public sealed record ReservationArrivalAlertDomainEvent(
    Guid ReservationId,
    Guid TableId,
    int TableNumber,
    Guid? GuestId,
    int PartySize) : DomainEventBase;

/// <summary>Raised when a reservation is cancelled, freeing the table's time slot.</summary>
public sealed record ReservationCancelledDomainEvent(Guid ReservationId, Guid TableId) : DomainEventBase;
