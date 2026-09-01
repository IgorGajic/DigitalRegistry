using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetOrders;

internal sealed class GetOrdersQueryHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTimeService)
    : IRequestHandler<GetOrdersQuery, Result<IReadOnlyList<OrderSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<OrderSummaryDto>>> Handle(
        GetOrdersQuery request,
        CancellationToken cancellationToken)
    {
        // The day's own bills are what the screen opens on, so an unqualified call answers that
        // rather than every order the venue has ever taken.
        var from = request.From ?? dateTimeService.TodayUtc.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To ?? dateTimeService.UtcNow;

        var query = context.Orders
            .AsNoTracking()
            .Where(order => order.CreatedAt >= from && order.CreatedAt <= to);

        if (request.Status is { } status)
        {
            query = query.Where(order => order.Status == status);
        }

        if (request.TableId is { } tableId)
        {
            query = query.Where(order => order.TableId == tableId);
        }

        var orders = await query
            .OrderByDescending(order => order.CreatedAt)
            .Take(request.Take)
            .Select(order => new
            {
                order.Id,
                order.TableId,
                TableNumber = order.Table!.TableNumber,
                order.Status,
                order.WaiterId,
                WaiterFirstName = order.Waiter == null ? null : order.Waiter.FirstName,
                WaiterLastName = order.Waiter == null ? null : order.Waiter.LastName,
                order.CreatedAt,
                ItemCount = order.OrderItems.Sum(item => item.Quantity),
                Total = order.OrderItems.Sum(item => item.UnitPrice * item.Quantity)
            })
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(order => order.Id).ToList();

        // The payment is a second row, and only the non-reversing one is the payment itself; the
        // counter-entry a reversal writes carries a negative amount and must not be read as one.
        var payments = await context.Transactions
            .AsNoTracking()
            .Where(transaction =>
                orderIds.Contains(transaction.OrderId) && transaction.ReversesTransactionId == null)
            .Select(transaction => new
            {
                transaction.OrderId,
                transaction.TransactionDate,
                transaction.PaymentMethod
            })
            .ToListAsync(cancellationToken);

        var paymentByOrder = payments
            .GroupBy(payment => payment.OrderId)
            .ToDictionary(group => group.Key, group => group.OrderBy(p => p.TransactionDate).First());

        var summaries = orders
            .Select(order =>
            {
                paymentByOrder.TryGetValue(order.Id, out var payment);

                return new OrderSummaryDto(
                    Id: order.Id,
                    // The same short form the receipt prints, so a guest can quote one and the desk
                    // can find it here.
                    Number: order.Id.ToString("N")[..8].ToUpperInvariant(),
                    TableId: order.TableId,
                    TableNumber: order.TableNumber,
                    Status: order.Status,
                    PlacedByGuest: order.WaiterId is null,
                    ServedBy: order.WaiterId is null
                        ? null
                        : $"{order.WaiterFirstName} {order.WaiterLastName}".Trim(),
                    CreatedAt: order.CreatedAt,
                    PaidAtUtc: payment?.TransactionDate,
                    PaymentMethod: payment?.PaymentMethod,
                    ItemCount: order.ItemCount,
                    Total: decimal.Round(order.Total, 2),
                    IsReversed: order.Status == OrderStatus.Voided);
            })
            .ToList();

        return Result<IReadOnlyList<OrderSummaryDto>>.Success(summaries);
    }
}
