using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Inventory.Commands.RecordStockEntry;

internal sealed class RecordStockEntryCommandHandler(
    IDigitalRegistryDbContext context,
    IInventoryAllocator inventoryAllocator,
    ICurrentUserService currentUser,
    IDateTimeService dateTime)
    : IRequestHandler<RecordStockEntryCommand, Result<StockEntryDto>>
{
    public async Task<Result<StockEntryDto>> Handle(
        RecordStockEntryCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Result<StockEntryDto>.Unauthorized("The member of staff receiving this could not be identified.");
        }

        var ingredient = await context.Ingredients
            .FirstOrDefaultAsync(candidate => candidate.Id == request.IngredientId, cancellationToken);

        if (ingredient is null)
        {
            return Result<StockEntryDto>.NotFound("No such ingredient.");
        }

        var entry = new StockEntry
        {
            RestaurantId = ingredient.RestaurantId,
            IngredientId = ingredient.Id,
            Quantity = request.Quantity,
            PurchaseUnitPrice = request.PurchaseUnitPrice,
            TotalCost = request.TotalCost ?? decimal.Round(request.Quantity * request.PurchaseUnitPrice, 2),
            Supplier = request.Supplier?.Trim(),
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            Note = request.Note?.Trim(),
            RecordedByUserId = userId,
            EntryDateUtc = request.EntryDateUtc ?? dateTime.UtcNow
        };

        // Folds the price into the moving average and raises the quantity.
        ingredient.Receive(request.Quantity, request.PurchaseUnitPrice);

        context.StockEntries.Add(entry);

        context.StockMovements.Add(new StockMovement
        {
            RestaurantId = ingredient.RestaurantId,
            IngredientId = ingredient.Id,
            Type = StockMovementType.Purchase,
            Quantity = request.Quantity,
            BalanceAfter = ingredient.StockQuantity,
            StockEntryId = entry.Id,
            RecordedByUserId = userId,
            OccurredAtUtc = entry.EntryDateUtc
        });

        // A delivery can bring an item back onto the menu, so availability is re-evaluated in the same
        // transaction — the same rule a sale follows, in the opposite direction.
        await inventoryAllocator.RefreshMenuAvailabilityAsync([ingredient.Id], cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        var recordedBy = await context.Users
            .Where(user => user.Id == userId)
            .Select(user => user.FirstName + " " + user.LastName)
            .FirstOrDefaultAsync(cancellationToken);

        return Result<StockEntryDto>.Success(new StockEntryDto(
            Id: entry.Id,
            IngredientId: ingredient.Id,
            IngredientName: ingredient.Name,
            Quantity: entry.Quantity,
            Unit: ingredient.Unit,
            PurchaseUnitPrice: entry.PurchaseUnitPrice,
            TotalCost: entry.TotalCost,
            Supplier: entry.Supplier,
            ReferenceNumber: entry.ReferenceNumber,
            Note: entry.Note,
            RecordedBy: recordedBy?.Trim() ?? string.Empty,
            EntryDateUtc: entry.EntryDateUtc,
            StockAfter: ingredient.StockQuantity,
            AveragePurchasePriceAfter: ingredient.AveragePurchasePrice));
    }
}
