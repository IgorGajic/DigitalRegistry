using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DigitalRegistry.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> construct the context without booting the web host.
/// </summary>
/// <remarks>
/// Used only by the migrations tooling. It resolves the connection string from configuration, so
/// migrations are generated against the same database settings the application runs with; the
/// literal fallback exists purely so the tooling still works on a clean checkout.
/// </remarks>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string FallbackConnectionString =
        "Server=localhost\\SQLEXPRESS;Initial Catalog=DigitalRegistry;Integrated Security=SSPI;" +
        "MultipleActiveResultSets=true;TrustServerCertificate=True;";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? FallbackConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sqlServer =>
                sqlServer.MigrationsAssembly(typeof(DesignTimeDbContextFactory).Assembly.FullName));

        return new ApplicationDbContext(
            optionsBuilder.Options,
            NullDomainEventDispatcher.Instance,
            NullTenantContext.Instance);
    }
}
