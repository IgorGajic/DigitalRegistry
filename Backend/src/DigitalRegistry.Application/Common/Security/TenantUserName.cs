namespace DigitalRegistry.Application.Common.Security;

/// <summary>
/// Builds the user name that makes an email address unique per restaurant.
/// </summary>
/// <remarks>
/// ASP.NET Core Identity requires a globally unique user name, but the same person may legitimately
/// work at two venues under one email address. Rather than replacing Identity's user store, the
/// restaurant's slug is prefixed onto the user name — <c>kafana-x|marko@example.com</c> — while the
/// <c>Email</c> column keeps the plain address for display and correspondence.
/// <para>
/// <see cref="Separator"/> is not in Identity's default allowed-character set, so
/// <c>AddIdentityCore</c> is configured to permit it. That is deliberate: no email address or slug can
/// contain the character, which is what makes the composition unambiguous.
/// </para>
/// </remarks>
public static class TenantUserName
{
    /// <summary>Divides the restaurant slug from the email address.</summary>
    public const char Separator = '|';

    /// <summary>The characters Identity must accept for composite user names to be storable.</summary>
    public const string AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+" + "|";

    /// <summary>Composes the Identity user name for an account at a given restaurant.</summary>
    public static string For(string restaurantSlug, string email) =>
        $"{NormalizeSlug(restaurantSlug)}{Separator}{email.Trim()}";

    /// <summary>
    /// Slugs are compared and stored lower-case so that a manager typing <c>Kafana-X</c> reaches the
    /// same account as <c>kafana-x</c>.
    /// </summary>
    public static string NormalizeSlug(string restaurantSlug) =>
        restaurantSlug.Trim().ToLowerInvariant();
}
