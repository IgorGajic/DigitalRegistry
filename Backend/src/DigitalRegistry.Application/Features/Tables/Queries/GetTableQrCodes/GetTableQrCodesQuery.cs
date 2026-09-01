using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Tables.Queries.GetTableQrCodes;

/// <summary>
/// Every table's QR token, for printing the codes that go on the tables.
/// </summary>
/// <remarks>
/// The tokens exist and the guest screen behind them works, but until they can be printed nobody in
/// the restaurant can reach it: there is no other way to get the link onto a table. Fetching them a
/// table at a time through <c>GET /api/tables/{id}</c> would mean one request per table and no way
/// to lay a room's codes out on one sheet.
/// </remarks>
/// <param name="RoomId">Restrict to one room, or null for the whole venue.</param>
/// <param name="IncludeInactive">
/// Include tables that are out of service. Off by default: a code printed for a table nobody is
/// seated at only produces sessions that cannot order.
/// </param>
public record GetTableQrCodesQuery(Guid? RoomId = null, bool IncludeInactive = false)
    : IRequest<Result<IReadOnlyList<TableQrCodeSheetEntryDto>>>;
