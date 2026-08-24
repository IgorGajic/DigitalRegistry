using DigitalRegistry.Application.Common.Interfaces;

namespace DigitalRegistry.Application.UnitTests.TestDoubles;

/// <summary>
/// A tenant context fixed to one restaurant for the duration of a test.
/// </summary>
/// <remarks>
/// The restaurant is mutable so a test can prove isolation: seed data as one restaurant, switch, and
/// assert the same context now sees nothing. That is the same instance the DbContext holds, which is
/// exactly how the production filter behaves — it reads the value per query rather than capturing it.
/// </remarks>
internal sealed class TestTenantContext(Guid restaurantId) : ITenantContext
{
    /// <summary>The restaurant tests use unless they say otherwise.</summary>
    public static readonly Guid DefaultRestaurantId = new("11111111-1111-1111-1111-111111111111");

    public Guid RestaurantId { get; set; } = restaurantId;

    public bool HasTenant => RestaurantId != Guid.Empty;
}
