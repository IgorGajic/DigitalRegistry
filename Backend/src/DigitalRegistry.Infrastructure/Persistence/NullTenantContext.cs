using DigitalRegistry.Application.Common.Interfaces;

namespace DigitalRegistry.Infrastructure.Persistence;

/// <summary>
/// A tenant context that resolves to no restaurant.
/// </summary>
/// <remarks>
/// Used by the migrations design-time factory, by tests, and by the master application. Because
/// <see cref="RestaurantId"/> matches no stored row, every restaurant-scoped query returns nothing
/// until the caller explicitly opts out with <c>IgnoreQueryFilters()</c> — which is the intended
/// shape for the master application: seeing across tenants has to be deliberate and visible in the
/// code that does it.
/// </remarks>
public sealed class NullTenantContext : ITenantContext
{
    public static readonly NullTenantContext Instance = new();

    public Guid RestaurantId => Guid.Empty;

    public bool HasTenant => false;
}
