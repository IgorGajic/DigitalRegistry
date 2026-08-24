using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.ValueObjects;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A subscribing venue — the tenant of the system.
/// </summary>
/// <remarks>
/// Created and maintained only from the master application. Every other entity in the schema is
/// scoped to one of these via <see cref="IRestaurantScoped"/>; this type is not, because the master
/// application needs to enumerate all of them.
/// </remarks>
public class Restaurant : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short, human-typeable code identifying the venue at sign-in, for example <c>kod-restorana</c>.
    /// </summary>
    /// <remarks>
    /// Staff type this alongside their email, which is what lets the same email address exist in
    /// more than one restaurant. Unique across the platform, lower-case by convention.
    /// </remarks>
    public string Slug { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? ContactEmail { get; set; }

    public string? PhoneNumber { get; set; }

    /// <summary>Currency every amount in this restaurant is denominated in.</summary>
    public string CurrencyCode { get; set; } = Money.DefaultCurrencyCode;

    /// <summary>IANA or Windows time zone id, used when reports group by local business day.</summary>
    public string TimeZoneId { get; set; } = "Europe/Belgrade";

    /// <summary>
    /// Cleared by the master application to switch a venue off regardless of its licence, for
    /// example when the contract ends. Licence expiry is tracked separately on <see cref="License"/>.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Every term the venue has ever bought, newest last.</summary>
    public ICollection<License> Licenses { get; set; } = new List<License>();
}
