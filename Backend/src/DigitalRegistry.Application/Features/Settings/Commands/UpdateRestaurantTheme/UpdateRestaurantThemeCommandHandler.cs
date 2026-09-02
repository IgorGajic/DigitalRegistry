using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Settings.Commands.UpdateRestaurantTheme;

public class UpdateRestaurantThemeCommandHandler(
    IDigitalRegistryDbContext context,
    ITenantContext tenant)
    : IRequestHandler<UpdateRestaurantThemeCommand, Result<RestaurantSettingsDto>>
{
    public async Task<Result<RestaurantSettingsDto>> Handle(
        UpdateRestaurantThemeCommand request,
        CancellationToken cancellationToken)
    {
        if (!tenant.HasTenant)
        {
            return Result<RestaurantSettingsDto>.Forbidden(
                "This endpoint is only meaningful for restaurant staff.");
        }

        var restaurant = await context.Restaurants
            .FirstOrDefaultAsync(candidate => candidate.Id == tenant.RestaurantId, cancellationToken);

        if (restaurant is null)
        {
            return Result<RestaurantSettingsDto>.NotFound("The restaurant on this token no longer exists.");
        }

        restaurant.Theme = request.Theme;

        await context.SaveChangesAsync(cancellationToken);

        return Result<RestaurantSettingsDto>.Success(
            new RestaurantSettingsDto(restaurant.Name, restaurant.Theme));
    }
}
