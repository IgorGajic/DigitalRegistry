using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform.Commands.IssueLicense;

public class IssueLicenseCommandHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUser,
    IDateTimeService dateTime)
    : IRequestHandler<IssueLicenseCommand, Result<LicenseDto>>
{
    public async Task<Result<LicenseDto>> Handle(
        IssueLicenseCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return Result<LicenseDto>.Unauthorized("The issuing administrator could not be identified.");
        }

        var restaurantExists = await context.AllRestaurants()
            .AnyAsync(restaurant => restaurant.Id == request.RestaurantId, cancellationToken);

        if (!restaurantExists)
        {
            return Result<LicenseDto>.NotFound("No such restaurant.");
        }

        var hasLicense = await context.AllLicenses()
            .AnyAsync(license => license.RestaurantId == request.RestaurantId, cancellationToken);

        if (hasLicense)
        {
            return Result<LicenseDto>.Conflict(
                "This restaurant already holds a licence. Renew it instead of issuing a second one.");
        }

        var license = License.Issue(
            request.RestaurantId,
            request.Plan,
            request.Price,
            adminId,
            dateTime.UtcNow,
            request.Notes);

        context.Licenses.Add(license);
        await context.SaveChangesAsync(cancellationToken);

        var dtos = await context.AllLicenses()
            .Where(candidate => candidate.Id == license.Id)
            .ToDtosAsync(context, dateTime.UtcNow, cancellationToken);

        return Result<LicenseDto>.Success(dtos[0]);
    }
}
