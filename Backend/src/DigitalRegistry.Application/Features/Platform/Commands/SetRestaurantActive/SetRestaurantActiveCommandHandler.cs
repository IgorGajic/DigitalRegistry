using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform.Commands.SetRestaurantActive;

public class SetRestaurantActiveCommandHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTime)
    : IRequestHandler<SetRestaurantActiveCommand, Result<RestaurantSummaryDto>>
{
    public async Task<Result<RestaurantSummaryDto>> Handle(
        SetRestaurantActiveCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await context.AllRestaurants()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (restaurant is null)
        {
            return Result<RestaurantSummaryDto>.NotFound("No such restaurant.");
        }

        restaurant.IsActive = request.IsActive;
        await context.SaveChangesAsync(cancellationToken);

        var summary = await context.SummariseAsync(
            context.AllRestaurants().Where(candidate => candidate.Id == restaurant.Id),
            dateTime.UtcNow,
            cancellationToken);

        return Result<RestaurantSummaryDto>.Success(summary[0]);
    }
}
