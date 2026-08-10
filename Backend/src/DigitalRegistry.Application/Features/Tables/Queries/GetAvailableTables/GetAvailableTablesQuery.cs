using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Tables.Queries.GetAvailableTables;

/// <summary>
/// Finds tables that can seat a party over a given period.
/// </summary>
/// <param name="PartySize">Number of guests; tables smaller than this are excluded.</param>
/// <param name="From">Start of the wanted period.</param>
/// <param name="To">End of the wanted period.</param>
/// <param name="IncludeUnavailable">
/// When true, big-enough tables that are reserved or occupied are returned as well, each carrying
/// its status, so a floor view can show the whole plan rather than only the free tables.
/// </param>
public record GetAvailableTablesQuery(
    int PartySize,
    DateTime From,
    DateTime To,
    bool IncludeUnavailable = false) : IRequest<Result<IReadOnlyList<TableAvailabilityDto>>>;
