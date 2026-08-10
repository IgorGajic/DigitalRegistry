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
}
