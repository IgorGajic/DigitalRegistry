using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Menu.Queries.GetMenuItem;

public class GetMenuItemQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetMenuItemQuery, Result<MenuItemDetailDto>>
{
    public async Task<Result<MenuItemDetailDto>> Handle(
        GetMenuItemQuery request,
        CancellationToken cancellationToken)
    {
        var menuItem = await context.MenuItems
            .AsNoTracking()
            .Include(candidate => candidate.Recipe)
            .ThenInclude(line => line.Ingredient)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        return menuItem is null
            ? Result<MenuItemDetailDto>.NotFound("No such menu item.")
            : Result<MenuItemDetailDto>.Success(menuItem.ToDetailDto());
    }
}
