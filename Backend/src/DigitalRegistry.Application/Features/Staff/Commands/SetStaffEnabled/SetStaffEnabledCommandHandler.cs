using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Staff.Commands.SetStaffEnabled;

public class SetStaffEnabledCommandHandler(
    IDigitalRegistryDbContext context,
    IIdentityService identityService,
    ICurrentUserService currentUser,
    ITenantContext tenant)
    : IRequestHandler<SetStaffEnabledCommand, Result>
{
    public async Task<Result> Handle(SetStaffEnabledCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == currentUser.UserId && !request.IsEnabled)
        {
            // Nothing else stops an owner locking themselves out of their own venue.
            return Result.Conflict("You cannot switch off your own account.");
        }

        // Checked against the token's restaurant before Identity is touched: Identity knows nothing
        // about tenancy and would happily lock out somebody at another venue.
        var belongsHere = await context.Users
            .AnyAsync(
                user => user.Id == request.Id && user.RestaurantId == tenant.RestaurantId,
                cancellationToken);

        if (!belongsHere)
        {
            return Result.NotFound("No such member of staff at this restaurant.");
        }

        return await identityService.SetAccountEnabledAsync(
            request.Id,
            request.IsEnabled,
            cancellationToken);
    }
}
