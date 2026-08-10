using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Inventory.Commands.RestockIngredient;

internal sealed class RestockIngredientCommandHandler(
    IDigitalRegistryDbContext context,
    IInventoryAllocator inventoryAllocator)
    : IRequestHandler<RestockIngredientCommand, Result<IngredientStockDto>>
{
    public async Task<Result<IngredientStockDto>> Handle(
        RestockIngredientCommand request,
        CancellationToken cancellationToken)
    {
        var ingredient = await context.Ingredients
            .FirstOrDefaultAsync(candidate => candidate.Id == request.IngredientId, cancellationToken);

        if (ingredient is null)
        {
            return Result<IngredientStockDto>.NotFound($"Ingredient {request.IngredientId} was not found.");
        }

        ingredient.Restock(request.Quantity);

        // Anything taken off the menu for want of this ingredient can come back now.
        await inventoryAllocator.RefreshMenuAvailabilityAsync([ingredient.Id], cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return Result<IngredientStockDto>.Success(new IngredientStockDto(
            ingredient.Id,
            ingredient.Name,
            ingredient.StockQuantity,
            ingredient.Unit,
            ingredient.LowStockThreshold,
            ingredient.IsLowOnStock));
    }
}
