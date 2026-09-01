using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reservations.Commands.CreateReservation;

/// <summary>
/// Books a table, either for the calling guest or, when the desk takes it, for somebody named.
/// </summary>
/// <remarks>
/// There is still no guest id on the command, and there deliberately never will be: a caller cannot
/// nominate whose account a booking lands on. What staff may do instead is book for a person with no
/// account at all, by writing down their name — which is how nearly every booking a restaurant takes
/// actually arrives. Such a booking belongs to the venue, not to the member of staff who answered
/// the telephone, and their id is recorded separately as who took it.
/// </remarks>
/// <param name="ContactName">
/// Who the table is for. Staff only, and required of them; a guest booking for themselves leaves it
/// null and the booking goes on their own account.
/// </param>
/// <param name="ContactPhone">A number to reach the party on. Optional, and staff only.</param>
public record CreateReservationCommand(
    Guid TableId,
    DateTime StartTime,
    DateTime EndTime,
    int PartySize,
    string? ContactName = null,
    string? ContactPhone = null) : IRequest<Result<ReservationDto>>;
