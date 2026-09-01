using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetTableSessionOrders;

internal sealed class GetTableSessionOrdersQueryHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetTableSessionOrdersQuery, Result<TableTabDto>>
{
    /// <summary>The statuses a tab passes through while it is still the guest's to pay.</summary>
    private static readonly OrderStatus[] StillRunning =
        [OrderStatus.Open, OrderStatus.InPreparation, OrderStatus.Served];

    public async Task<Result<TableTabDto>> Handle(
        GetTableSessionOrdersQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.TableId is not { } tableId)
        {
            return Result<TableTabDto>.Forbidden(
                "This endpoint answers a table QR session; scan the code on the table first.");
        }

        var table = await context.Tables
            .AsNoTracking()
            .Where(candidate => candidate.Id == tableId)
            .Select(candidate => new { candidate.TableNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (table is null)
        {
            return Result<TableTabDto>.NotFound("The table this session belongs to no longer exists.");
        }

        // Settled, cancelled and reversed tabs are left out: this answers "what have we had so far",
        // and a bill already paid is no longer part of that.
        var orders = await context.Orders
            .AsNoTracking()
            .Where(order => order.TableId == tableId && StillRunning.Contains(order.Status))
            .OrderBy(order => order.CreatedAt)
            .Select(order => new TableTabRoundDto(
                order.Id,
                order.CreatedAt,
                order.Status,
                order.WaiterId == null,
                order.OrderItems
                    .OrderBy(item => item.MenuItem!.Name)
                    .Select(item => new TableTabLineDto(
                        item.MenuItem!.Name,
                        item.Quantity,
                        item.UnitPrice,
                        item.UnitPrice * item.Quantity,
                        item.Notes))
                    .ToList()))
            .ToListAsync(cancellationToken);

        var lines = orders.SelectMany(order => order.Lines).ToList();

        return Result<TableTabDto>.Success(new TableTabDto(
            TableId: tableId,
            TableNumber: table.TableNumber,
            ItemCount: lines.Sum(line => line.Quantity),
            Total: decimal.Round(lines.Sum(line => line.LineTotal), 2),
            Rounds: orders));
    }
}
