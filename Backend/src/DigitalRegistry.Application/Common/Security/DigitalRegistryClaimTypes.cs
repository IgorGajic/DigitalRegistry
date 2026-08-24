namespace DigitalRegistry.Application.Common.Security;

/// <summary>
/// Names of the custom claims this system puts in its tokens.
/// </summary>
/// <remarks>
/// Shared between the Infrastructure code that mints tokens and the API code that reads them, so
/// the two can never drift apart over a misspelled string.
/// </remarks>
public static class DigitalRegistryClaimTypes
{
    /// <summary>The <see cref="Domain.Enums.UserRole"/> name. Also emitted as the standard role claim.</summary>
    public const string Role = "digitalregistry:role";

    /// <summary>
    /// Present only on anonymous table-session tokens minted from a QR scan, and pins every action
    /// the session takes to this one table.
    /// </summary>
    public const string TableId = "digitalregistry:table_id";

    /// <summary>The human-readable table number, carried for display purposes only.</summary>
    public const string TableNumber = "digitalregistry:table_number";

    /// <summary>
    /// The restaurant every request made with this token is confined to.
    /// </summary>
    /// <remarks>
    /// Present on staff tokens and on anonymous table-session tokens alike — a QR session without it
    /// would resolve to no tenant and slip past the global query filters.
    /// Absent only on platform administrator tokens, which belong to no restaurant.
    /// </remarks>
    public const string RestaurantId = "digitalregistry:restaurant_id";

    /// <summary>The restaurant's sign-in code, carried for display purposes only.</summary>
    public const string RestaurantSlug = "digitalregistry:restaurant_slug";
}
