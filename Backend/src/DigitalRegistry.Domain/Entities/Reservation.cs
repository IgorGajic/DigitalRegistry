using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Domain.Exceptions;
using DigitalRegistry.Domain.ValueObjects;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A guest's claim on a table for a period of time.
/// </summary>
public class Reservation : AggregateRoot, IRestaurantScoped
{
    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    /// <summary>
    /// The account the booking belongs to, or null for one the desk wrote down on somebody's behalf.
    /// </summary>
    /// <remarks>
    /// Nullable because most bookings in a restaurant arrive by telephone, from a guest who has no
    /// account and never will. Filing those under the member of staff who answered the phone would
    /// put the wrong name on the service sheet and let that member of staff cancel them as if they
    /// were their own, so a desk booking has no guest account at all — it carries
    /// <see cref="ContactName"/> instead.
    /// </remarks>
    public Guid? GuestId { get; set; }

    /// <summary>
    /// Who the table is held for, as the desk wrote it down. Null when a guest booked it themselves,
    /// in which case the name comes from their account.
    /// </summary>
    public string? ContactName { get; set; }

    /// <summary>A telephone number for the booking, so the desk can call about a late party.</summary>
    public string? ContactPhone { get; set; }

    /// <summary>The member of staff who took the booking, or null when the guest booked it.</summary>
    public Guid? TakenByUserId { get; set; }

    public Guid TableId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int PartySize { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public Table? Table { get; set; }

    public ApplicationUser? Guest { get; set; }

    public ApplicationUser? TakenBy { get; set; }

    /// <summary>The booked period, which owns the double-booking rule.</summary>
    public ShiftTimeRange TimeRange => new(StartTime, EndTime);

    /// <summary>
    /// Reservations only hold a table while they are pending or confirmed; cancelled and completed
    /// ones no longer block the slot.
    /// </summary>
    public bool BlocksTable => Status is ReservationStatus.Pending or ReservationStatus.Confirmed;

    public bool Overlaps(ShiftTimeRange other) => TimeRange.Overlaps(other);

    public void Confirm()
    {
        if (Status is not ReservationStatus.Pending)
        {
            throw new DomainException($"Only a pending reservation can be confirmed; this one is {Status}.");
        }

        Status = ReservationStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status is ReservationStatus.Cancelled)
        {
            return;
        }

        if (Status is ReservationStatus.Completed)
        {
            throw new DomainException("A completed reservation cannot be cancelled.");
        }

        Status = ReservationStatus.Cancelled;
        RaiseDomainEvent(new ReservationCancelledDomainEvent(Id, TableId));
    }

    /// <summary>
    /// Records the guest arriving, which completes the reservation and alerts the floor.
    /// </summary>
    /// <param name="table">The reserved table, needed so the alert can name the table number.</param>
    public void MarkArrived(Table table)
    {
        if (!BlocksTable)
        {
            throw new DomainException($"A {Status} reservation cannot be checked in.");
        }

        Status = ReservationStatus.Completed;
        RaiseDomainEvent(new ReservationArrivalAlertDomainEvent(Id, TableId, table.TableNumber, GuestId, PartySize));
    }
}
