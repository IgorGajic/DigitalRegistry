using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Reservations;

/// <summary>A booking as seen by the guest who made it.</summary>
public record ReservationDto(
    Guid Id,
    Guid TableId,
    int TableNumber,
    DateTime StartTime,
    DateTime EndTime,
    int PartySize,
    ReservationStatus Status);

/// <summary>
/// A booking on the day's service sheet, including who it is for.
/// </summary>
/// <remarks>Staff-facing: carries the guest's name, so it is never returned to other guests.</remarks>
public record ReservationScheduleEntryDto(
    Guid Id,
    Guid TableId,
    int TableNumber,
    DateTime StartTime,
    DateTime EndTime,
    int PartySize,
    ReservationStatus Status,
    Guid GuestId,
    string GuestName);
