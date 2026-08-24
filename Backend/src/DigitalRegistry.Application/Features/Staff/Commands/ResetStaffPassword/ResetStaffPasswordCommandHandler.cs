using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Staff.Commands.ResetStaffPassword;

public class ResetStaffPasswordCommandHandler(
    IDigitalRegistryDbContext context,
    IIdentityService identityService,
    ITenantContext tenant)
    : IRequestHandler<ResetStaffPasswordCommand, Result>
{
    public async Task<Result> Handle(ResetStaffPasswordCommand request, CancellationToken cancellationToken)
    {
        var belongsHere = await context.Users
            .AnyAsync(
                user => user.Id == request.Id && user.RestaurantId == tenant.RestaurantId,
                cancellationToken);

        if (!belongsHere)
        {
            return Result.NotFound("No such member of staff at this restaurant.");
        }

        return await identityService.ResetPasswordAsync(request.Id, request.NewPassword, cancellationToken);
    }
}
