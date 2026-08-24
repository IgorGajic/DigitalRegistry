using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Security;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace DigitalRegistry.Api.Shared.Services;

/// <summary>
/// Resolves the current restaurant from the validated bearer token.
/// </summary>
/// <remarks>
/// The value comes from a signed claim, never from a route, header or request body, so a caller
/// cannot reach another venue's data by editing the request. Staff tokens and anonymous QR table
/// sessions both carry the claim; requests without one resolve to no tenant, and the global query
/// filters then return nothing.
/// </remarks>
public sealed class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid RestaurantId =>
        Guid.TryParse(
            httpContextAccessor.HttpContext?.User.FindFirstValue(DigitalRegistryClaimTypes.RestaurantId),
            out var restaurantId)
            ? restaurantId
            : Guid.Empty;

    public bool HasTenant => RestaurantId != Guid.Empty;
}
