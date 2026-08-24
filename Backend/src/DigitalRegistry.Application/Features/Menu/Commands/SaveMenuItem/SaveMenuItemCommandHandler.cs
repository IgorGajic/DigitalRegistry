using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Menu.Commands.SaveMenuItem;

public class SaveMenuItemCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<SaveMenuItemCommand, Result<MenuItemDetailDto>>
{
    public async Task<Result<MenuItemDetailDto>> Handle(
        SaveMenuItemCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var nameTaken = await context.MenuItems
            .AnyAsync(other => other.Name == name && other.Id != request.Id, cancellationToken);

        if (nameTaken)
        {
            return Result<MenuItemDetailDto>.Conflict($"'{name}' is already on the menu.");
        }

        MenuItem menuItem;

        if (request.Id is { } id)
        {
            var existing = await context.MenuItems
                .Include(candidate => candidate.Recipe)
                .ThenInclude(line => line.Ingredient)
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            if (existing is null)
            {
                return Result<MenuItemDetailDto>.NotFound("No such menu item.");
            }

            menuItem = existing;
        }
        else
        {
            menuItem = new MenuItem();
            context.MenuItems.Add(menuItem);
        }

        menuItem.Name = name;
        menuItem.Category = request.Category.Trim();
        menuItem.UnitPrice = request.UnitPrice;

        // Raises the availability event only when the flag actually flips. Note that the stock guard
        // can override this downwards on the next movement: an item whose ingredients have run out
        // comes off the menu whatever a manager set here.
        menuItem.SetAvailability(request.IsAvailable);

        await context.SaveChangesAsync(cancellationToken);

        return Result<MenuItemDetailDto>.Success(menuItem.ToDetailDto());
    }
}
