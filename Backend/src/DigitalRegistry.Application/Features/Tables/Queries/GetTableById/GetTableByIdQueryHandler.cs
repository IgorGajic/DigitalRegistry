using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Tables.Queries.GetTableById;

public class GetTableByIdQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetTableByIdQuery, Result<TableDto>>
{
    public async Task<Result<TableDto>> Handle(GetTableByIdQuery request, CancellationToken cancellationToken)
    {
        var table = await context.Tables
            .AsNoTracking()
            .Where(candidate => candidate.Id == request.Id)
            .Select(candidate => new TableDto(
                candidate.Id,
                candidate.TableNumber,
                candidate.Capacity,
                candidate.QrCodeToken,
                candidate.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return table is null
            ? Result<TableDto>.NotFound($"Table {request.Id} was not found.")
            : Result<TableDto>.Success(table);
    }
}
