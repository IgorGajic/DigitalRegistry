using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Queries.GetRestaurants;

public class GetRestaurantsQueryHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTime)
    : IRequestHandler<GetRestaurantsQuery, Result<IReadOnlyList<RestaurantSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<RestaurantSummaryDto>>> Handle(
        GetRestaurantsQuery request,
        CancellationToken cancellationToken)
    {
        var restaurants = context.AllRestaurants();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            restaurants = restaurants.Where(restaurant =>
                restaurant.Name.Contains(search) || restaurant.Slug.Contains(search));
        }

        if (request.IsActive is { } isActive)
        {
            restaurants = restaurants.Where(restaurant => restaurant.IsActive == isActive);
        }

        var summaries = await context.SummariseAsync(restaurants, dateTime.UtcNow, cancellationToken);

        // Applied after projection, not in SQL: expiry is derived from the date rather than stored,
        // so the database has no column to filter on.
        if (request.LicenseStatus is { } status)
        {
            summaries = summaries.Where(summary => summary.LicenseStatus == status).ToList();
        }

        // Whatever is closest to lapsing needs attention first.
        var ordered = summaries
            .OrderBy(summary => summary.LicenseExpiresAtUtc ?? DateTime.MaxValue)
            .ThenBy(summary => summary.Name)
            .ToList();

        return Result<IReadOnlyList<RestaurantSummaryDto>>.Success(ordered);
    }
}
