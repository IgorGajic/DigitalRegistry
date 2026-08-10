using System.ComponentModel.DataAnnotations;

namespace DigitalRegistry.Infrastructure.Identity;

/// <summary>
/// Token issuance settings, bound from the <c>Jwt</c> configuration section.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>Minimum key length in bytes. HMAC-SHA256 requires a key at least this long.</summary>
    public const int MinimumSigningKeyBytes = 32;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// The HMAC signing secret. Must never be committed for a deployed environment: supply it
    /// through user secrets, an environment variable or a key vault.
    /// </summary>
    [Required]
    [MinLength(MinimumSigningKeyBytes)]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Lifetime of a token issued to a signed-in user.</summary>
    [Range(1, 1440)]
    public int AccessTokenLifetimeMinutes { get; set; } = 480;

    /// <summary>
    /// Lifetime of an anonymous table-session token. Shorter than a staff token because it is handed
    /// out to anyone who can photograph the table's QR code, so a leaked one should expire quickly.
    /// </summary>
    [Range(1, 1440)]
    public int TableSessionLifetimeMinutes { get; set; } = 180;
}
