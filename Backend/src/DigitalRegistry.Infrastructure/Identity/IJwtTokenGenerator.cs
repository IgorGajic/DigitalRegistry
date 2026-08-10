using DigitalRegistry.Domain.Entities;

namespace DigitalRegistry.Infrastructure.Identity;

/// <summary>
/// Builds signed access tokens. Internal to Infrastructure: the Application layer asks
/// <see cref="Application.Common.Interfaces.IIdentityService"/> for a token and never sees JWT types.
/// </summary>
internal interface IJwtTokenGenerator
{
    /// <summary>Issues a token for a signed-in user, carrying their id, email and role.</summary>
    (string Token, DateTime ExpiresAtUtc) GenerateForUser(ApplicationUser user);

    /// <summary>
    /// Issues an anonymous token scoped to a single table, carrying the guest role and the table id
    /// but no user identity.
    /// </summary>
    (string Token, DateTime ExpiresAtUtc) GenerateForTableSession(Guid tableId, int tableNumber);
}
