using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Settings.Queries.GetRestaurantSettings;

public class GetRestaurantSettingsQueryHandler(
    IDigitalRegistryDbContext context,
    ITenantContext tenant)
    : IRequestHandler<GetRestaurantSettingsQuery, Result<RestaurantSettingsDto>>
{
    public async Task<Result<RestaurantSettingsDto>> Handle(
        GetRestaurantSettingsQuery request,
        CancellationToken cancellationToken)
    {
        if (!tenant.HasTenant)
        {
            return Result<RestaurantSettingsDto>.Forbidden(
                "This endpoint is only meaningful for restaurant staff.");
        }

        // Restaurants is not restaurant-scoped, so there is no filter to lean on: the lookup is by
        // the id on the caller's own token, like any other foreign key.
        var settings = await context.Restaurants
            .AsNoTracking()
            .Where(restaurant => restaurant.Id == tenant.RestaurantId)
            .Select(restaurant => new RestaurantSettingsDto(restaurant.Name, restaurant.Theme))
            .FirstOrDefaultAsync(cancellationToken);

        return settings is null
            ? Result<RestaurantSettingsDto>.NotFound("The restaurant on this token no longer exists.")
            : Result<RestaurantSettingsDto>.Success(settings);
    }
}
