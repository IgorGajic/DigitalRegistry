using DigitalRegistry.Domain.Entities;

namespace DigitalRegistry.Infrastructure.Identity;

/// <summary>
/// Builds signed access tokens. Internal to Infrastructure: the Application layer asks
/// <see cref="Application.Common.Interfaces.IIdentityService"/> for a token and never sees JWT types.
/// </summary>
internal interface IJwtTokenGenerator
{
    /// <summary>
    /// Issues a token for a signed-in user, carrying their id, email, role and restaurant.
    /// </summary>
    /// <param name="restaurantSlug">Carried for display only; the id is what confines the session.</param>
    (string Token, DateTime ExpiresAtUtc) GenerateForUser(ApplicationUser user, string? restaurantSlug = null);

    /// <summary>
    /// Issues an anonymous token scoped to a single table, carrying the guest role, the restaurant
    /// and the table id but no user identity.
    /// </summary>
    (string Token, DateTime ExpiresAtUtc) GenerateForTableSession(Guid restaurantId, Guid tableId, int tableNumber);
}
