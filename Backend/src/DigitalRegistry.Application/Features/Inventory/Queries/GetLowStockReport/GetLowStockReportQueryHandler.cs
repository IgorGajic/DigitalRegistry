using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Inventory.Queries.GetLowStockReport;

internal sealed class GetLowStockReportQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetLowStockReportQuery, Result<IReadOnlyList<LowStockReportEntryDto>>>
{
    public async Task<Result<IReadOnlyList<LowStockReportEntryDto>>> Handle(
        GetLowStockReportQuery request,
        CancellationToken cancellationToken)
    {
        var report = await context.Ingredients
            .AsNoTracking()
            .Where(ingredient => ingredient.StockQuantity <= ingredient.LowStockThreshold)
            // Ordered on the entity, before projecting, so the comparison translates to SQL rather
            // than being applied to already-materialised DTOs. Deepest shortfall relative to its own
            // threshold comes first, which is comparable across differing units of measure.
            .OrderBy(ingredient => ingredient.LowStockThreshold == 0
                ? ingredient.StockQuantity
                : ingredient.StockQuantity / ingredient.LowStockThreshold)
            .ThenBy(ingredient => ingredient.Name)
            .Select(ingredient => new LowStockReportEntryDto(
                ingredient.Id,
                ingredient.Name,
                ingredient.StockQuantity,
                ingredient.Unit,
                ingredient.LowStockThreshold,
                // Only items already off the menu, since those are the ones restocking would fix.
                ingredient.UsedIn
                    .Where(recipeItem => !recipeItem.MenuItem!.IsAvailable)
                    .Select(recipeItem => recipeItem.MenuItem!.Name)
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<LowStockReportEntryDto>>.Success(report);
    }
}
