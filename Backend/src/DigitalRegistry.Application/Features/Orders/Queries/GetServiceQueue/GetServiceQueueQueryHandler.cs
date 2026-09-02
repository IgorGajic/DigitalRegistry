using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetServiceQueue;

public class GetServiceQueueQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetServiceQueueQuery, Result<IReadOnlyList<ServiceTicketDto>>>
{
    public async Task<Result<IReadOnlyList<ServiceTicketDto>>> Handle(
        GetServiceQueueQuery request,
        CancellationToken cancellationToken)
    {
        var tickets = await context.Orders
            .AsNoTracking()
            // A guest order is one with nobody attached: it arrived from a phone, not from a pad.
            .Where(order => order.WaiterId == null)
            // Served is left out, not Paid: the tab stays open long after the drinks land, and a
            // ticket is about the tray, not the money.
            .Where(order => order.Status == OrderStatus.Open
                            || order.Status == OrderStatus.InPreparation)
            // Oldest first. This is a queue, and the table that has been waiting longest is the one
            // that should be served next — which is the whole reason for showing it as a list.
            .OrderBy(order => order.CreatedAt)
            .Select(order => new ServiceTicketDto(
                Id: order.Id,
                TableId: order.TableId,
                TableNumber: order.Table!.TableNumber,
                RoomName: order.Table.Room != null ? order.Table.Room.Name : null,
                PlacedAtUtc: order.CreatedAt,
                Items: order.OrderItems
                    .Where(item => item.Quantity > 0)
                    .Select(item => new ServiceTicketLineDto(item.MenuItem!.Name, item.Quantity))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ServiceTicketDto>>.Success(tickets);
    }
}
