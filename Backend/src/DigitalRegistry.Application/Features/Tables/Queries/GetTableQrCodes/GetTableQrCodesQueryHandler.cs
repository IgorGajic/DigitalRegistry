using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Tables.Queries.GetTableQrCodes;

internal sealed class GetTableQrCodesQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetTableQrCodesQuery, Result<IReadOnlyList<TableQrCodeSheetEntryDto>>>
{
    public async Task<Result<IReadOnlyList<TableQrCodeSheetEntryDto>>> Handle(
        GetTableQrCodesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Tables.AsNoTracking();

        if (request.RoomId is { } roomId)
        {
            query = query.Where(table => table.RoomId == roomId);
        }

        if (!request.IncludeInactive)
        {
            query = query.Where(table => table.IsActive);
        }

        var entries = await query
            // Room order first, then table number: the sheet comes out in the order somebody walking
            // the room would tape the codes down.
            .OrderBy(table => table.Room == null ? int.MaxValue : table.Room.DisplayOrder)
            .ThenBy(table => table.TableNumber)
            .Select(table => new TableQrCodeSheetEntryDto(
                table.Id,
                table.TableNumber,
                table.Capacity,
                table.RoomId,
                table.Room == null ? null : table.Room.Name,
                table.QrCodeToken,
                table.IsActive))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<TableQrCodeSheetEntryDto>>.Success(entries);
    }
}
