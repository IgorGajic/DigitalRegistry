using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetServiceQueue;

public class GetServiceQueueQueryHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTime)
    : IRequestHandler<GetServiceQueueQuery, Result<ServiceQueueDto>>
{
    /// <summary>
    /// How far back the "just went out" list reaches.
    /// </summary>
    /// <remarks>
    /// A press meant for another table is noticed within a minute or two, not within a shift. Long
    /// enough to cover a slip and a walk back to the till; short enough that the list stays a handful
    /// of cards rather than the day's history, which is what the bills screen is for.
    /// </remarks>
    private static readonly TimeSpan RecentWindow = TimeSpan.FromMinutes(30);

    /// <summary>Nobody scrolls an undo list. Anything older than these is found on /racuni.</summary>
    private const int RecentLimit = 6;

    public async Task<Result<ServiceQueueDto>> Handle(
        GetServiceQueueQuery request,
        CancellationToken cancellationToken)
    {
        var since = dateTime.UtcNow - RecentWindow;

        // Guest orders only: a waiter who took an order at the table already knows about it.
        var guestOrders = context.Orders.AsNoTracking().Where(order => order.WaiterId == null);

        var waiting = await guestOrders
            .Where(order => order.Status == OrderStatus.Open
                            || order.Status == OrderStatus.InPreparation)
            // Oldest first: this is a queue, and the table that has waited longest goes next.
            .OrderBy(order => order.CreatedAt)
            .Select(Project)
            .ToListAsync(cancellationToken);

        var recentlyServed = await guestOrders
            .Where(order => order.Status == OrderStatus.Served && order.ServedAtUtc >= since)
            // Newest first: the one most likely to have been a slip is the one just pressed.
            .OrderByDescending(order => order.ServedAtUtc)
            .Take(RecentLimit)
            .Select(Project)
            .ToListAsync(cancellationToken);

        return Result<ServiceQueueDto>.Success(new ServiceQueueDto(waiting, recentlyServed));
    }

    /// <summary>Shared so the two lists cannot drift into showing different things.</summary>
    private static System.Linq.Expressions.Expression<Func<Order, ServiceTicketDto>> Project =>
        order => new ServiceTicketDto(
            order.Id,
            order.TableId,
            order.Table!.TableNumber,
            order.Table.Room != null ? order.Table.Room.Name : null,
            order.CreatedAt,
            order.ServedAtUtc,
            order.OrderItems
                .Where(item => item.Quantity > 0)
                .Select(item => new ServiceTicketLineDto(item.MenuItem!.Name, item.Quantity))
                .ToList());
}
