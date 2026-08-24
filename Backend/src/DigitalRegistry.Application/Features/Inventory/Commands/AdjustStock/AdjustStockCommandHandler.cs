using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Inventory.Commands.AdjustStock;

internal sealed class AdjustStockCommandHandler(
    IDigitalRegistryDbContext context,
    IInventoryAllocator inventoryAllocator,
    ICurrentUserService currentUser,
    IDateTimeService dateTime)
    : IRequestHandler<AdjustStockCommand, Result<StockAdjustmentResultDto>>
{
    public async Task<Result<StockAdjustmentResultDto>> Handle(
        AdjustStockCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<StockAdjustmentResultDto>.Unauthorized(
                "The member of staff adjusting this could not be identified.");
        }

        var ingredient = await context.Ingredients
            .FirstOrDefaultAsync(candidate => candidate.Id == request.IngredientId, cancellationToken);

        if (ingredient is null)
        {
            return Result<StockAdjustmentResultDto>.NotFound("No such ingredient.");
        }

        var previous = ingredient.StockQuantity;

        decimal difference;

        try
        {
            difference = ingredient.AdjustTo(request.CountedQuantity);
        }
        catch (DomainException exception)
        {
            return Result<StockAdjustmentResultDto>.Invalid(exception.Message);
        }

        if (difference == 0)
        {
            // A count that matches the books is not a movement, and writing a zero-quantity ledger
            // line for it would only add noise to the consumption report.
            return Result<StockAdjustmentResultDto>.Success(Describe(ingredient, previous, request, 0m));
        }

        context.StockMovements.Add(new StockMovement
        {
            RestaurantId = ingredient.RestaurantId,
            IngredientId = ingredient.Id,
            Type = StockMovementType.Adjustment,
            Quantity = difference,
            BalanceAfter = ingredient.StockQuantity,
            Note = request.Reason.Trim(),
            RecordedByUserId = userId,
            OccurredAtUtc = dateTime.UtcNow
        });

        // A correction in either direction can take an item off the menu or put it back.
        await inventoryAllocator.RefreshMenuAvailabilityAsync([ingredient.Id], cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return Result<StockAdjustmentResultDto>.Success(Describe(ingredient, previous, request, difference));
    }

    private static StockAdjustmentResultDto Describe(
        Ingredient ingredient,
        decimal previous,
        AdjustStockCommand request,
        decimal difference) => new(
        IngredientId: ingredient.Id,
        IngredientName: ingredient.Name,
        PreviousQuantity: previous,
        CountedQuantity: request.CountedQuantity,
        Difference: difference,
        Unit: ingredient.Unit,
        IsLowOnStock: ingredient.IsLowOnStock);
}
