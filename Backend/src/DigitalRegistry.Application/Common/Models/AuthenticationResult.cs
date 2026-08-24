using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Common.Models;

/// <summary>
/// A freshly issued access token and the identity it represents.
/// </summary>
/// <param name="AccessToken">The signed JWT to send as a bearer token.</param>
/// <param name="ExpiresAtUtc">When the token stops being accepted.</param>
/// <param name="UserId">The authenticated user, or null for an anonymous table session.</param>
/// <param name="Email">The user's email, or null for an anonymous table session.</param>
/// <param name="FullName">Display name, or null for an anonymous table session.</param>
/// <param name="Role">The role encoded in the token.</param>
/// <param name="RestaurantId">
/// The restaurant every request made with this token is confined to. Null only for platform
/// administrators, who belong to no restaurant.
/// </param>
/// <param name="RestaurantSlug">The restaurant's sign-in code, for display.</param>
/// <param name="TableId">
/// Set only on tokens minted by scanning a table QR code, and pins the session to that table.
/// </param>
public record AuthenticationResult(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid? UserId,
    string? Email,
    string? FullName,
    UserRole Role,
    Guid? RestaurantId = null,
    string? RestaurantSlug = null,
    Guid? TableId = null);
