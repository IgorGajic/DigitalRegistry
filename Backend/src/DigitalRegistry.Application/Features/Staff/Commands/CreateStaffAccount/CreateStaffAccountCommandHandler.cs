using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Common.Security;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Staff.Commands.CreateStaffAccount;

public class CreateStaffAccountCommandHandler(
    IDigitalRegistryDbContext context,
    IIdentityService identityService,
    ITenantContext tenant,
    IDateTimeService dateTime)
    : IRequestHandler<CreateStaffAccountCommand, Result<StaffMemberDto>>
{
    public async Task<Result<StaffMemberDto>> Handle(
        CreateStaffAccountCommand request,
        CancellationToken cancellationToken)
    {
        // Restaurants are not restaurant-scoped, so this is an ordinary lookup by the id the token
        // carries rather than anything the caller supplied.
        var slug = await context.Restaurants
            .Where(restaurant => restaurant.Id == tenant.RestaurantId)
            .Select(restaurant => restaurant.Slug)
            .FirstOrDefaultAsync(cancellationToken);

        if (slug is null)
        {
            return Result<StaffMemberDto>.NotFound("The restaurant on this token no longer exists.");
        }

        var created = await identityService.CreateAccountAsync(
            tenant.RestaurantId,
            slug,
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            request.Role,
            cancellationToken);

        if (!created.Succeeded)
        {
            return Result<StaffMemberDto>.Failure(created.ErrorType, created.Errors.ToArray());
        }

        return Result<StaffMemberDto>.Success(new StaffMemberDto(
            Id: created.Value,
            FullName: $"{request.FirstName} {request.LastName}".Trim(),
            Email: request.Email,
            UserName: TenantUserName.For(slug, request.Email),
            Role: request.Role,
            IsEnabled: true,
            Created: dateTime.UtcNow));
    }
}
