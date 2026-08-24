using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Menu.Commands.SetRecipe;

public class SetRecipeCommandHandler(
    IDigitalRegistryDbContext context,
    IInventoryAllocator inventoryAllocator)
    : IRequestHandler<SetRecipeCommand, Result<MenuItemDetailDto>>
{
    public async Task<Result<MenuItemDetailDto>> Handle(
        SetRecipeCommand request,
        CancellationToken cancellationToken)
    {
        var menuItem = await context.MenuItems
            .FirstOrDefaultAsync(candidate => candidate.Id == request.MenuItemId, cancellationToken);

        if (menuItem is null)
        {
            return Result<MenuItemDetailDto>.NotFound("No such menu item.");
        }

        var requestedIds = request.Lines.Select(line => line.IngredientId).ToList();

        // Loaded through the tenant filter, so a recipe naming another restaurant's ingredient finds
        // nothing and is reported as unknown rather than silently linking across venues.
        var ingredients = await context.Ingredients
            .Where(ingredient => requestedIds.Contains(ingredient.Id))
            .ToDictionaryAsync(ingredient => ingredient.Id, cancellationToken);

        var unknown = requestedIds.Where(id => !ingredients.ContainsKey(id)).ToList();

        if (unknown.Count > 0)
        {
            return Result<MenuItemDetailDto>.NotFound(
                $"The recipe names {unknown.Count} ingredient(s) that do not belong to this restaurant.");
        }

        // Loaded through the DbSet rather than the item's navigation, and never touching that
        // collection. Removing entities while also clearing the navigation makes EF Core handle the
        // same rows twice — once as deletes, once as orphans — and the second pass tries to delete
        // rows the first has already removed, which surfaces as a concurrency failure.
        var existing = await context.RecipeItems
            .Where(line => line.MenuItemId == menuItem.Id)
            .ToListAsync(cancellationToken);

        var previousIngredientIds = existing.Select(line => line.IngredientId).ToList();

        context.RecipeItems.RemoveRange(existing);

        foreach (var line in request.Lines)
        {
            context.RecipeItems.Add(new RecipeItem
            {
                RestaurantId = menuItem.RestaurantId,
                MenuItemId = menuItem.Id,
                IngredientId = line.IngredientId,
                QuantityRequired = line.QuantityRequired
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        // Availability is refreshed after the recipe is stored, and covers the ingredients that were
        // dropped as well as the new ones: an item freed from a depleted ingredient has to come back
        // onto the menu, not only be taken off it.
        await inventoryAllocator.RefreshMenuAvailabilityAsync(
            previousIngredientIds.Union(requestedIds).ToArray(),
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        var saved = await context.MenuItems
            .AsNoTracking()
            .Include(candidate => candidate.Recipe)
            .ThenInclude(line => line.Ingredient)
            .FirstAsync(candidate => candidate.Id == menuItem.Id, cancellationToken);

        return Result<MenuItemDetailDto>.Success(saved.ToDetailDto());
    }
}
