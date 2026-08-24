using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform.Commands.CreateRestaurant;

public class CreateRestaurantCommandHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTime)
    : IRequestHandler<CreateRestaurantCommand, Result<RestaurantSummaryDto>>
{
    public async Task<Result<RestaurantSummaryDto>> Handle(
        CreateRestaurantCommand request,
        CancellationToken cancellationToken)
    {
        var slug = TenantUserName.NormalizeSlug(request.Slug);

        if (await context.AllRestaurants().AnyAsync(existing => existing.Slug == slug, cancellationToken))
        {
            return Result<RestaurantSummaryDto>.Conflict($"The restaurant code '{slug}' is already taken.");
        }

        var restaurant = new Restaurant
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Address = request.Address?.Trim(),
            ContactEmail = request.ContactEmail?.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? Money.DefaultCurrencyCode
                : request.CurrencyCode.Trim().ToUpperInvariant(),
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId)
                ? "Europe/Belgrade"
                : request.TimeZoneId.Trim(),
            IsActive = true
        };

        context.Restaurants.Add(restaurant);
        await context.SaveChangesAsync(cancellationToken);

        var summary = await context.SummariseAsync(
            context.AllRestaurants().Where(candidate => candidate.Id == restaurant.Id),
            dateTime.UtcNow,
            cancellationToken);

        return Result<RestaurantSummaryDto>.Success(summary[0]);
    }
}
