using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Staff.Commands.UpdateStaffAccount;

public class UpdateStaffAccountCommandHandler(
    IDigitalRegistryDbContext context,
    ITenantContext tenant,
    IDateTimeService dateTime)
    : IRequestHandler<UpdateStaffAccountCommand, Result<StaffMemberDto>>
{
    public async Task<Result<StaffMemberDto>> Handle(
        UpdateStaffAccountCommand request,
        CancellationToken cancellationToken)
    {
        // Users are not restaurant-scoped, so the tenant is checked here rather than being applied
        // by a query filter.
        var user = await context.Users
            .FirstOrDefaultAsync(
                candidate => candidate.Id == request.Id
                             && candidate.RestaurantId == tenant.RestaurantId,
                cancellationToken);

        if (user is null)
        {
            return Result<StaffMemberDto>.NotFound("No such member of staff at this restaurant.");
        }

        if (user.Role == UserRole.Owner && request.Role != UserRole.Owner)
        {
            // Demoting the owner would leave the venue with nobody able to manage it.
            return Result<StaffMemberDto>.Conflict("The role of the owner cannot be changed.");
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();

        if (user.Role != UserRole.Owner)
        {
            user.Role = request.Role;
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result<StaffMemberDto>.Success(
            user.ToDto(new DateTimeOffset(dateTime.UtcNow, TimeSpan.Zero)));
    }
}
