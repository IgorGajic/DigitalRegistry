using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Queries.GetLicenses;

public class GetLicensesQueryHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTime)
    : IRequestHandler<GetLicensesQuery, Result<IReadOnlyList<LicenseDto>>>
{
    public async Task<Result<IReadOnlyList<LicenseDto>>> Handle(
        GetLicensesQuery request,
        CancellationToken cancellationToken)
    {
        var licenses = context.AllLicenses();

        if (request.RestaurantId is { } restaurantId)
        {
            licenses = licenses.Where(license => license.RestaurantId == restaurantId);
        }

        var dtos = await licenses
            .OrderByDescending(license => license.ExpiresAtUtc)
            .ToDtosAsync(context, dateTime.UtcNow, cancellationToken);

        // Filtered after projection because expiry is derived from the date rather than stored.
        if (request.Status is { } status)
        {
            dtos = dtos.Where(dto => dto.Status == status).ToList();
        }

        return Result<IReadOnlyList<LicenseDto>>.Success(dtos);
    }
}
