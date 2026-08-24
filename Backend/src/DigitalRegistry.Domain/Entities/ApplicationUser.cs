using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A person who can sign in: guest, waiter, manager or owner.
/// </summary>
/// <remarks>
/// Derives from <see cref="IdentityUser{TKey}"/> as the specification requires, which is the one
/// place the Domain layer touches an external package. <see cref="Role"/> is stored on the user
/// row for querying (for example "is this user a waiter?") while the same value is also mirrored
/// into an ASP.NET Core Identity role so that <c>[Authorize(Roles = ...)]</c> works.
/// </remarks>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    /// <summary>
    /// The restaurant this account belongs to; null only for <see cref="UserRole.PlatformAdmin"/>.
    /// </summary>
    /// <remarks>
    /// Not an <see cref="IRestaurantScoped"/> implementation on purpose. Identity owns this table and
    /// queries it through its own stores, which a global query filter would silently narrow — sign-in
    /// would then fail for anyone outside the ambient tenant, including before a tenant is known.
    /// Tenant separation for users is enforced by the composite user name instead (see
    /// <c>IdentityService</c>).
    /// </remarks>
    public Guid? RestaurantId { get; set; }

    public Restaurant? Restaurant { get; set; }

    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}
