using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Queries.GetRestaurantById;

public class GetRestaurantByIdQueryHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTime)
    : IRequestHandler<GetRestaurantByIdQuery, Result<RestaurantSummaryDto>>
{
    public async Task<Result<RestaurantSummaryDto>> Handle(
        GetRestaurantByIdQuery request,
        CancellationToken cancellationToken)
    {
        var summaries = await context.SummariseAsync(
            context.AllRestaurants().Where(restaurant => restaurant.Id == request.Id),
            dateTime.UtcNow,
            cancellationToken);

        return summaries.Count == 0
            ? Result<RestaurantSummaryDto>.NotFound("No such restaurant.")
            : Result<RestaurantSummaryDto>.Success(summaries[0]);
    }
}
