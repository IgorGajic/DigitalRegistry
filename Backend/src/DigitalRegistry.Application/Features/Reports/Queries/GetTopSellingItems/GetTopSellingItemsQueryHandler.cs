using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetTopSellingItems;

public class GetTopSellingItemsQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetTopSellingItemsQuery, Result<IReadOnlyList<TopSellingItemDto>>>
{
    public async Task<Result<IReadOnlyList<TopSellingItemDto>>> Handle(
        GetTopSellingItemsQuery request,
        CancellationToken cancellationToken)
    {
        // Paid orders only. A tab that was cancelled or reversed never became revenue, and including
        // its lines would let anyone inflate their numbers by ringing items up and voiding them.
        var lines = context.OrderItems
            .AsNoTracking()
            .Where(item => item.Order!.Status == OrderStatus.Paid
                           && item.Order.CreatedAt >= request.FromUtc
                           && item.Order.CreatedAt < request.ToUtc);

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = request.Category.Trim();
            lines = lines.Where(item => item.MenuItem!.Category == category);
        }

        var grouped = await lines
            .GroupBy(item => new { item.MenuItemId, item.MenuItem!.Name, item.MenuItem.Category })
            .Select(group => new
            {
                group.Key.MenuItemId,
                group.Key.Name,
                group.Key.Category,
                QuantitySold = group.Sum(item => item.Quantity),
                Revenue = group.Sum(item => item.UnitPrice * item.Quantity)
            })
            .OrderByDescending(row => row.Revenue)
            .Take(Math.Clamp(request.Top, 1, 200))
            .ToListAsync(cancellationToken);

        var menuItemIds = grouped.Select(row => row.MenuItemId).ToList();

        // What one serving of each costs to make, at the ingredients' moving average price. Loaded
        // separately rather than joined into the grouping, which SQL would not translate.
        var unitCosts = await context.RecipeItems
            .AsNoTracking()
            .Where(line => menuItemIds.Contains(line.MenuItemId))
            .GroupBy(line => line.MenuItemId)
            .Select(group => new
            {
                MenuItemId = group.Key,
                Cost = group.Sum(line => line.QuantityRequired * line.Ingredient!.AveragePurchasePrice)
            })
            .ToDictionaryAsync(row => row.MenuItemId, row => row.Cost, cancellationToken);

        var results = grouped
            .Select(row =>
            {
                // Null rather than zero when nothing is known about cost: a margin computed against a
                // cost of zero reads as pure profit, which is flattering and false.
                decimal? cost = unitCosts.TryGetValue(row.MenuItemId, out var unitCost) && unitCost > 0
                    ? decimal.Round(unitCost * row.QuantitySold, 2)
                    : null;

                return new TopSellingItemDto(
                    MenuItemId: row.MenuItemId,
                    Name: row.Name,
                    Category: row.Category,
                    QuantitySold: row.QuantitySold,
                    Revenue: decimal.Round(row.Revenue, 2),
                    EstimatedCost: cost,
                    EstimatedMargin: cost is { } known ? decimal.Round(row.Revenue - known, 2) : null);
            })
            .ToList();

        return Result<IReadOnlyList<TopSellingItemDto>>.Success(results);
    }
}
