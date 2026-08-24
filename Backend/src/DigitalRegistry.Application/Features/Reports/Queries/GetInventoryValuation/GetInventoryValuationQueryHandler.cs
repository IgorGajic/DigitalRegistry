using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetInventoryValuation;

public class GetInventoryValuationQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetInventoryValuationQuery, Result<InventoryValuationDto>>
{
    public async Task<Result<InventoryValuationDto>> Handle(
        GetInventoryValuationQuery request,
        CancellationToken cancellationToken)
    {
        var ingredients = await context.Ingredients
            .AsNoTracking()
            .OrderBy(ingredient => ingredient.Name)
            .Select(ingredient => new
            {
                ingredient.Id,
                ingredient.Name,
                ingredient.Unit,
                ingredient.StockQuantity,
                ingredient.AveragePurchasePrice,
                ingredient.LowStockThreshold
            })
            .ToListAsync(cancellationToken);

        // The ledger is what makes consumption answerable at all: the ingredient row only knows what
        // is left, not what went through it.
        var movements = await context.StockMovements
            .AsNoTracking()
            .Where(movement => movement.OccurredAtUtc >= request.FromUtc
                               && movement.OccurredAtUtc < request.ToUtc)
            .GroupBy(movement => new { movement.IngredientId, movement.Type })
            .Select(group => new
            {
                group.Key.IngredientId,
                group.Key.Type,
                Quantity = group.Sum(movement => movement.Quantity)
            })
            .ToListAsync(cancellationToken);

        // What deliveries in the period actually cost. Taken from the entries rather than valued at
        // today's average price: the average moves with every delivery, so re-pricing past purchases
        // through it answers "what would this cost now", not "what did we pay" — and the second is
        // the only one an owner can check against invoices.
        var purchaseCosts = await context.StockEntries
            .AsNoTracking()
            .Where(entry => entry.EntryDateUtc >= request.FromUtc && entry.EntryDateUtc < request.ToUtc)
            .GroupBy(entry => entry.IngredientId)
            .Select(group => new
            {
                IngredientId = group.Key,
                Cost = group.Sum(entry => entry.TotalCost)
            })
            .ToDictionaryAsync(row => row.IngredientId, row => row.Cost, cancellationToken);

        var byIngredient = movements
            .GroupBy(movement => movement.IngredientId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(row => row.Type, row => row.Quantity));

        var lines = new List<InventoryValuationLineDto>();

        foreach (var ingredient in ingredients)
        {
            var isLow = ingredient.StockQuantity <= ingredient.LowStockThreshold;

            if (request.LowStockOnly && !isLow)
            {
                continue;
            }

            var types = byIngredient.GetValueOrDefault(ingredient.Id, []);

            // Sales are stored negative and returns positive, so netting them gives what was really
            // used: a drink poured and then cancelled consumed nothing.
            var sold = -types.GetValueOrDefault(StockMovementType.Sale);
            var returned = types.GetValueOrDefault(StockMovementType.Return);
            var consumed = sold - returned;

            var purchased = types.GetValueOrDefault(StockMovementType.Purchase);
            var adjusted = types.GetValueOrDefault(StockMovementType.Adjustment);

            lines.Add(new InventoryValuationLineDto(
                IngredientId: ingredient.Id,
                Name: ingredient.Name,
                Unit: ingredient.Unit,
                StockQuantity: ingredient.StockQuantity,
                AveragePurchasePrice: ingredient.AveragePurchasePrice,
                StockValue: decimal.Round(ingredient.StockQuantity * ingredient.AveragePurchasePrice, 2),
                LowStockThreshold: ingredient.LowStockThreshold,
                IsLowOnStock: isLow,
                ConsumedQuantity: consumed,
                ConsumedValue: decimal.Round(consumed * ingredient.AveragePurchasePrice, 2),
                PurchasedQuantity: purchased,
                // Zero when stock came in through a bare restock, which records no price. That is
                // the honest answer: nothing was entered as paid for it.
                PurchasedValue: decimal.Round(purchaseCosts.GetValueOrDefault(ingredient.Id), 2),
                AdjustedQuantity: adjusted));
        }

        return Result<InventoryValuationDto>.Success(new InventoryValuationDto(
            FromUtc: request.FromUtc,
            ToUtc: request.ToUtc,
            TotalStockValue: decimal.Round(lines.Sum(line => line.StockValue), 2),
            TotalConsumedValue: decimal.Round(lines.Sum(line => line.ConsumedValue), 2),
            TotalPurchasedValue: decimal.Round(lines.Sum(line => line.PurchasedValue), 2),
            LowStockCount: lines.Count(line => line.IsLowOnStock),
            Lines: lines));
    }
}
