using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Tables.Commands.CreateTable;

public class CreateTableCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<CreateTableCommand, Result<TableDto>>
{
    public async Task<Result<TableDto>> Handle(CreateTableCommand request, CancellationToken cancellationToken)
    {
        // Checked here as well as by the unique index, so the caller gets a clear 409 rather than a
        // database constraint violation surfacing as a 500.
        var numberInUse = await context.Tables
            .AnyAsync(table => table.TableNumber == request.TableNumber, cancellationToken);

        if (numberInUse)
        {
            return Result<TableDto>.Conflict($"Table number {request.TableNumber} is already in use.");
        }

        var table = new Table
        {
            TableNumber = request.TableNumber,
            Capacity = request.Capacity,
            IsActive = true
        };

        context.Tables.Add(table);
        await context.SaveChangesAsync(cancellationToken);

        return Result<TableDto>.Success(new TableDto(
            table.Id,
            table.TableNumber,
            table.Capacity,
            table.QrCodeToken,
            table.IsActive));
    }
}
