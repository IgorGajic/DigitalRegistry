using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Inventory.Queries.GetStockEntries;

public class GetStockEntriesQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetStockEntriesQuery, Result<IReadOnlyList<StockEntryDto>>>
{
    public async Task<Result<IReadOnlyList<StockEntryDto>>> Handle(
        GetStockEntriesQuery request,
        CancellationToken cancellationToken)
    {
        var entries = context.StockEntries
            .AsNoTracking()
            .Where(entry => entry.EntryDateUtc >= request.FromUtc && entry.EntryDateUtc < request.ToUtc);

        if (request.IngredientId is { } ingredientId)
        {
            entries = entries.Where(entry => entry.IngredientId == ingredientId);
        }

        var results = await entries
            .OrderByDescending(entry => entry.EntryDateUtc)
            .Select(entry => new StockEntryDto(
                entry.Id,
                entry.IngredientId,
                entry.Ingredient!.Name,
                entry.Quantity,
                entry.Ingredient.Unit,
                entry.PurchaseUnitPrice,
                entry.TotalCost,
                entry.Supplier,
                entry.ReferenceNumber,
                entry.Note,
                (entry.RecordedBy!.FirstName + " " + entry.RecordedBy.LastName).Trim(),
                entry.EntryDateUtc,
                // The balance and average as they stand now, not as they stood at the delivery: the
                // ledger is where the historical position lives.
                entry.Ingredient.StockQuantity,
                entry.Ingredient.AveragePurchasePrice))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<StockEntryDto>>.Success(results);
    }
}
