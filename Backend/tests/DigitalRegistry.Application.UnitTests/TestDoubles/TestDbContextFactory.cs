using DigitalRegistry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.UnitTests.TestDoubles;

/// <summary>
/// Builds a throwaway in-memory <see cref="ApplicationDbContext"/> per test.
/// </summary>
/// <remarks>
/// Uses the real context, so the tests run against the same configured model — keys, relationships
/// and value conversions — that production uses, rather than a simplified stand-in. Each call gets a
/// uniquely named database so tests stay independent.
/// </remarks>
internal static class TestDbContextFactory
{
    public static ApplicationDbContext Create() => Create(out _);

    /// <summary>
    /// Builds the context and hands back the tenant it is bound to, so a test can move it to another
    /// restaurant and observe the global query filters take effect.
    /// </summary>
    public static ApplicationDbContext Create(out TestTenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"digitalregistry-tests-{Guid.NewGuid()}")
            .Options;

        tenantContext = new TestTenantContext(TestTenantContext.DefaultRestaurantId);

        // Domain events are not the subject of these tests, so they are discarded rather than
        // requiring MediatR to be wired up. The tenant is real, though: without one, the restaurant
        // filters would hide every row the test just inserted.
        return new ApplicationDbContext(options, NullDomainEventDispatcher.Instance, tenantContext);
    }
}
