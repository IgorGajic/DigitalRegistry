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
/// <param name="GuestId">
/// The account the booking belongs to, or null for one the desk took by telephone. Null is the
/// common case in a restaurant, so nothing may key off it being present.
/// </param>
/// <param name="GuestName">
/// Who to call for. Either the account holder's name or, for a desk booking, the name that was
/// written down — the sheet does not distinguish, because the person carrying it does not need to.
/// </param>
/// <param name="ContactPhone">A number for the party, when the desk took one.</param>
/// <param name="TakenBy">The member of staff who took the booking, or null when the guest made it.</param>
public record ReservationScheduleEntryDto(
    Guid Id,
    Guid TableId,
    int TableNumber,
    DateTime StartTime,
    DateTime EndTime,
    int PartySize,
    ReservationStatus Status,
    Guid? GuestId,
    string GuestName,
    string? ContactPhone,
    string? TakenBy);
