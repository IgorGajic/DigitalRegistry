using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Menu.Queries.GetMenu;

internal sealed class GetMenuQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetMenuQuery, Result<IReadOnlyList<MenuItemDto>>>
{
    public async Task<Result<IReadOnlyList<MenuItemDto>>> Handle(
        GetMenuQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.MenuItems.AsNoTracking();

        if (!request.IncludeUnavailable)
        {
            query = query.Where(menuItem => menuItem.IsAvailable);
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(menuItem => menuItem.Category == request.Category);
        }

        var menu = await query
            .OrderBy(menuItem => menuItem.Category)
            .ThenBy(menuItem => menuItem.Name)
            .Select(menuItem => new MenuItemDto(
                menuItem.Id,
                menuItem.Name,
                menuItem.Category,
                menuItem.UnitPrice,
                menuItem.IsAvailable))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<MenuItemDto>>.Success(menu);
    }
}
