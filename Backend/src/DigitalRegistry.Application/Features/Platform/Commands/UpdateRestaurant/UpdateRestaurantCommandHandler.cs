using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform.Commands.UpdateRestaurant;

public class UpdateRestaurantCommandHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTime)
    : IRequestHandler<UpdateRestaurantCommand, Result<RestaurantSummaryDto>>
{
    public async Task<Result<RestaurantSummaryDto>> Handle(
        UpdateRestaurantCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await context.AllRestaurants()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (restaurant is null)
        {
            return Result<RestaurantSummaryDto>.NotFound("No such restaurant.");
        }

        restaurant.Name = request.Name.Trim();
        restaurant.Address = request.Address?.Trim();
        restaurant.ContactEmail = request.ContactEmail?.Trim();
        restaurant.PhoneNumber = request.PhoneNumber?.Trim();

        if (!string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            restaurant.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.TimeZoneId))
        {
            restaurant.TimeZoneId = request.TimeZoneId.Trim();
        }

        await context.SaveChangesAsync(cancellationToken);

        var summary = await context.SummariseAsync(
            context.AllRestaurants().Where(candidate => candidate.Id == restaurant.Id),
            dateTime.UtcNow,
            cancellationToken);

        return Result<RestaurantSummaryDto>.Success(summary[0]);
    }
}
