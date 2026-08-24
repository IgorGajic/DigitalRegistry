using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Features.Auth.Commands.Login;
using DigitalRegistry.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DigitalRegistry.IntegrationTests;

/// <summary>
/// Boots the till API in-process, against an in-memory database seeded with the demo restaurant.
/// </summary>
/// <remarks>
/// The point of these tests is the wiring: routing, authentication, the RBAC policies, the licence
/// guard, the query filters, and the handlers talking to each other. That is what a unit test on a
/// handler cannot reach.
/// <para>
/// The provider is deliberately in-memory, so <c>dotnet test</c> needs nothing installed. It does
/// not enforce constraints or translate SQL, so it cannot stand in for a real database — that is
/// what <c>tools/api-walkthrough</c> is for, running the same endpoints against SQL Server.
/// </para>
/// </remarks>
public class DigitalRegistryApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"digitalregistry-tests-{Guid.NewGuid():N}";

    public const string RestaurantSlug = "demo";
    public const string DemoPassword = "Demo#Pass123";
    public const string OwnerEmail = "owner@digitalregistry.local";
    public const string ManagerEmail = "manager@digitalregistry.local";
    public const string WaiterEmail = "waiter@digitalregistry.local";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development, because that is the only environment in which the demo data is seeded — and
        // the demo restaurant with its menu, recipes, stock and licence is the fixture these tests
        // work against.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Never used: the provider is replaced below. It has to be present because the host
                // refuses to start without one.
                ["ConnectionStrings:DefaultConnection"] = "InMemory",
                ["SeedDemoData"] = "true"
            }));

        builder.ConfigureServices(services =>
        {
            // Adding a second provider is not enough: EF merges the options from every registration
            // and then refuses to start with two providers configured. The SQL Server registration
            // has to go first — including the options configuration `AddDbContext` leaves behind.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
        });
    }

    /// <summary>A client carrying a token for one of the demo accounts.</summary>
    public async Task<HttpClient> SignInAsync(string email)
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginCommand(RestaurantSlug, email, DemoPassword));

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AuthenticationResult>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", result!.AccessToken);

        return client;
    }

    /// <summary>
    /// Runs a query against the same database the API is using.
    /// </summary>
    /// <remarks>
    /// Assertions go through the context rather than through more API calls: a response can only
    /// repeat what the handler chose to return, while the rows say what was actually written.
    /// <para>
    /// There is no HTTP request here, so there is no tenant claim and the global query filters match
    /// nothing. Queries that expect rows have to say <c>IgnoreQueryFilters()</c> — which is honest,
    /// since a test is deliberately looking from outside any one restaurant.
    /// </para>
    /// </remarks>
    public async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return await query(context);
    }

    public async Task MutateAsync(Func<ApplicationDbContext, Task> mutate)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await mutate(context);
        await context.SaveChangesAsync();
    }
}
