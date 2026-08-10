using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Events;
using DigitalRegistry.Domain.Exceptions;
using DigitalRegistry.Domain.ValueObjects;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A guest's claim on a table for a period of time.
/// </summary>
public class Reservation : AggregateRoot
{
    public Guid GuestId { get; set; }

    public Guid TableId { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int PartySize { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public Table? Table { get; set; }

    public ApplicationUser? Guest { get; set; }

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
