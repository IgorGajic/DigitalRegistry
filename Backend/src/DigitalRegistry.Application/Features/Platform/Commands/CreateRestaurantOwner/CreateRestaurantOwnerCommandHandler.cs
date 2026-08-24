using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform.Commands.CreateRestaurantOwner;

public class CreateRestaurantOwnerCommandHandler(
    IDigitalRegistryDbContext context,
    IIdentityService identityService)
    : IRequestHandler<CreateRestaurantOwnerCommand, Result<CreatedUserDto>>
{
    public async Task<Result<CreatedUserDto>> Handle(
        CreateRestaurantOwnerCommand request,
        CancellationToken cancellationToken)
    {
        var restaurant = await context.AllRestaurants()
            .AsNoTracking()
            .Where(candidate => candidate.Id == request.RestaurantId)
            .Select(candidate => new { candidate.Id, candidate.Slug })
            .FirstOrDefaultAsync(cancellationToken);

        if (restaurant is null)
        {
            return Result<CreatedUserDto>.NotFound("No such restaurant.");
        }

        var alreadyHasOwner = await context.Users
            .AnyAsync(
                user => user.RestaurantId == restaurant.Id && user.Role == UserRole.Owner,
                cancellationToken);

        if (alreadyHasOwner)
        {
            return Result<CreatedUserDto>.Conflict(
                "This restaurant already has an owner. Further accounts are created by the owner.");
        }

        var created = await identityService.CreateAccountAsync(
            restaurant.Id,
            restaurant.Slug,
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            UserRole.Owner,
            cancellationToken);

        if (!created.Succeeded)
        {
            return Result<CreatedUserDto>.Failure(created.ErrorType, created.Errors.ToArray());
        }

        return Result<CreatedUserDto>.Success(new CreatedUserDto(
            Id: created.Value,
            Email: request.Email,
            // Returned because it is not simply the email: the owner has to know their sign-in also
            // needs the restaurant code.
            UserName: TenantUserName.For(restaurant.Slug, request.Email),
            FullName: $"{request.FirstName} {request.LastName}".Trim(),
            Role: UserRole.Owner));
    }
}
