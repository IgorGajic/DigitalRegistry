namespace DigitalRegistry.Domain.Common;

/// <summary>
/// Marks an entity as belonging to exactly one restaurant.
/// </summary>
/// <remarks>
/// Every type carrying this interface is automatically given an EF Core global query filter on
/// <see cref="RestaurantId"/> and has the value stamped on insert, both in the DbContext. Handlers
/// therefore never write <c>Where(x =&gt; x.RestaurantId == ...)</c> themselves: one restaurant's data
/// is invisible to another by construction rather than by remembering to filter.
/// <para>
/// Platform-level entities — <c>Restaurant</c>, <c>License</c>, <c>LicensePayment</c> — deliberately
/// do not implement this, because the master application must see across every tenant.
/// </para>
/// </remarks>
public interface IRestaurantScoped
{
    Guid RestaurantId { get; set; }
}
