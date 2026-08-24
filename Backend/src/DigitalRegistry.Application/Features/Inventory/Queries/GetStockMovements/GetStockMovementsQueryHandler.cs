using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Inventory.Queries.GetStockMovements;

public class GetStockMovementsQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetStockMovementsQuery, Result<IReadOnlyList<StockMovementDto>>>
{
    public async Task<Result<IReadOnlyList<StockMovementDto>>> Handle(
        GetStockMovementsQuery request,
        CancellationToken cancellationToken)
    {
        var movements = context.StockMovements
            .AsNoTracking()
            .Where(movement => movement.OccurredAtUtc >= request.FromUtc
                               && movement.OccurredAtUtc < request.ToUtc);

        if (request.IngredientId is { } ingredientId)
        {
            movements = movements.Where(movement => movement.IngredientId == ingredientId);
        }

        if (request.Type is { } type)
        {
            movements = movements.Where(movement => movement.Type == type);
        }

        var results = await movements
            .OrderByDescending(movement => movement.OccurredAtUtc)
            .Select(movement => new StockMovementDto(
                movement.Id,
                movement.IngredientId,
                movement.Ingredient!.Name,
                movement.Type,
                movement.Quantity,
                movement.BalanceAfter,
                movement.Ingredient.Unit,
                movement.OrderId,
                movement.Note,
                movement.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<StockMovementDto>>.Success(results);
    }
}
