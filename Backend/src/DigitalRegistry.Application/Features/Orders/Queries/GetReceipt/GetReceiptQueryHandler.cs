using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetReceipt;

public class GetReceiptQueryHandler(
    IDigitalRegistryDbContext context,
    ITenantContext tenant)
    : IRequestHandler<GetReceiptQuery, Result<ReceiptDto>>
{
    public async Task<Result<ReceiptDto>> Handle(GetReceiptQuery request, CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .AsNoTracking()
            .Include(candidate => candidate.Table)
            .Include(candidate => candidate.Waiter)
            .Include(candidate => candidate.OrderItems)
            .ThenInclude(item => item.MenuItem)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.OrderId, cancellationToken);

        if (order is null)
        {
            return Result<ReceiptDto>.NotFound("No such order.");
        }

        var restaurant = await context.Restaurants
            .AsNoTracking()
            .Where(candidate => candidate.Id == tenant.RestaurantId)
            .Select(candidate => new
            {
                candidate.Name,
                candidate.Address,
                candidate.PhoneNumber,
                candidate.CurrencyCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (restaurant is null)
        {
            return Result<ReceiptDto>.NotFound("The restaurant on this token no longer exists.");
        }

        var payment = await context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.OrderId == order.Id && transaction.ReversesTransactionId == null)
            .Select(transaction => new { transaction.TransactionDate, transaction.PaymentMethod })
            .FirstOrDefaultAsync(cancellationToken);

        var lines = order.OrderItems
            .OrderBy(item => item.MenuItem!.Name)
            .Select(item => new ReceiptLineDto(
                Name: item.MenuItem!.Name,
                Quantity: item.Quantity,
                UnitPrice: item.UnitPrice,
                LineTotal: decimal.Round(item.UnitPrice * item.Quantity, 2),
                Notes: item.Notes))
            .ToList();

        return Result<ReceiptDto>.Success(new ReceiptDto(
            OrderId: order.Id,
            // The first block of the order id, upper-cased. Enough for a guest to quote over the
            // phone, and honest about not being a sequential invoice number.
            Number: order.Id.ToString("N")[..8].ToUpperInvariant(),
            RestaurantName: restaurant.Name,
            RestaurantAddress: restaurant.Address,
            RestaurantPhone: restaurant.PhoneNumber,
            CurrencyCode: restaurant.CurrencyCode,
            TableNumber: order.Table?.TableNumber ?? 0,
            ServedBy: order.Waiter is null ? null : $"{order.Waiter.FirstName} {order.Waiter.LastName}".Trim(),
            OpenedAtUtc: order.CreatedAt,
            PaidAtUtc: payment?.TransactionDate,
            PaymentMethod: payment?.PaymentMethod,
            Status: order.Status,
            IsReversed: order.Status == OrderStatus.Voided,
            Total: decimal.Round(lines.Sum(line => line.LineTotal), 2),
            Lines: lines));
    }
}
