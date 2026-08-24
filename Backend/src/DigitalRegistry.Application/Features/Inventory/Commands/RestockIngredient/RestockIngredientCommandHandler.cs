using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Inventory.Commands.RestockIngredient;

internal sealed class RestockIngredientCommandHandler(
    IDigitalRegistryDbContext context,
    IInventoryAllocator inventoryAllocator,
    ICurrentUserService currentUser,
    IDateTimeService dateTime)
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

        // Every other path that moves stock leaves a ledger entry; this one has to as well. Without
        // it `SUM(Quantity)` stops reconciling with `StockQuantity`, and the difference shows up
        // later as an unexplained gap in the consumption report rather than as an error here.
        // No price and no `StockEntry`: this is the quick correction, not a recorded delivery — a
        // delivery with a cost goes through `RecordStockEntry`, which also moves the average price.
        context.StockMovements.Add(new StockMovement
        {
            RestaurantId = ingredient.RestaurantId,
            IngredientId = ingredient.Id,
            Type = StockMovementType.Purchase,
            Quantity = request.Quantity,
            BalanceAfter = ingredient.StockQuantity,
            RecordedByUserId = currentUser.UserId,
            OccurredAtUtc = dateTime.UtcNow
        });

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
