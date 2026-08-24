using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Menu.Commands.DeleteMenuItem;

public class DeleteMenuItemCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<DeleteMenuItemCommand, Result>
{
    public async Task<Result> Handle(DeleteMenuItemCommand request, CancellationToken cancellationToken)
    {
        var menuItem = await context.MenuItems
            .Include(candidate => candidate.Recipe)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (menuItem is null)
        {
            return Result.NotFound("No such menu item.");
        }

        var hasBeenOrdered = await context.OrderItems
            .AnyAsync(item => item.MenuItemId == menuItem.Id, cancellationToken);

        if (hasBeenOrdered)
        {
            return Result.Conflict(
                $"'{menuItem.Name}' appears on past orders and cannot be deleted. "
                + "Take it off the menu instead by clearing its availability.");
        }

        // The recipe has no meaning without its item and goes with it; the cascade is configured on
        // the relationship, but the lines are removed explicitly so the intent is visible here too.
        context.RecipeItems.RemoveRange(menuItem.Recipe);
        context.MenuItems.Remove(menuItem);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
