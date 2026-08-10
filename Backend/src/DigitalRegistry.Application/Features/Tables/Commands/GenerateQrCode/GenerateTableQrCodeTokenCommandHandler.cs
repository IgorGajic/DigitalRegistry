using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Tables.Commands.GenerateQrCode;

public class GenerateTableQrCodeTokenCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GenerateTableQrCodeTokenCommand, Result<TableQrCodeDto>>
{
    public async Task<Result<TableQrCodeDto>> Handle(
        GenerateTableQrCodeTokenCommand request,
        CancellationToken cancellationToken)
    {
        var table = await context.Tables
            .FirstOrDefaultAsync(candidate => candidate.Id == request.TableId, cancellationToken);

        if (table is null)
        {
            return Result<TableQrCodeDto>.NotFound($"Table {request.TableId} was not found.");
        }

        var token = table.RotateQrCodeToken();
        await context.SaveChangesAsync(cancellationToken);

        return Result<TableQrCodeDto>.Success(new TableQrCodeDto(table.Id, table.TableNumber, token));
    }
}
