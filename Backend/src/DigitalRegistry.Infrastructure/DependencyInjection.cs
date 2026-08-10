using System.Text;
using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Infrastructure.Identity;
using DigitalRegistry.Infrastructure.Persistence;
using DigitalRegistry.Infrastructure.RealTime;
using DigitalRegistry.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace DigitalRegistry.Infrastructure;

/// <summary>
/// Registers the Infrastructure layer: persistence, Identity, JWT authentication and services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Path prefix under which SignalR hubs are mapped.</summary>
    private const string HubPathPrefix = "/hubs";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddIdentityAndAuthentication(configuration);
        services.AddRealTimeNotifications();

        services.AddSingleton<IDateTimeService, DateTimeService>();

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:DefaultConnection' is not configured.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServer =>
            {
                sqlServer.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                // Transient network faults are common against a remote SQL instance; retrying is
                // cheaper than surfacing a 500 for a blip.
                sqlServer.EnableRetryOnFailure(maxRetryCount: 3, TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            }));

        // Handlers depend on the interface; both resolve to the same scoped instance so a handler and
        // the seeder share one unit of work.
        services.AddScoped<IDigitalRegistryDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ApplicationDbContextSeeder>();

        return services;
    }

    private static IServiceCollection AddIdentityAndAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // AddIdentityCore rather than AddIdentity: the latter also installs cookie authentication and
        // makes it the default scheme, which would fight with bearer tokens.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            // Fail at startup rather than on the first login attempt with a missing signing key.
            .ValidateOnStart();

        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                          ?? throw new InvalidOperationException(
                              $"Configuration section '{JwtSettings.SectionName}' is missing.");

        if (Encoding.UTF8.GetByteCount(jwtSettings.SigningKey) < JwtSettings.MinimumSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"'{JwtSettings.SectionName}:SigningKey' must be at least " +
                $"{JwtSettings.MinimumSigningKeyBytes} bytes long for HMAC-SHA256 signing.");
        }

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IIdentityService, IdentityService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                // Keep the claim names exactly as issued instead of remapping them to legacy
                // WS-Federation URIs, so the constants in DigitalRegistryClaimTypes still match.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = System.Security.Claims.ClaimTypes.Name
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Browsers cannot set an Authorization header on a WebSocket handshake, so
                        // SignalR passes the token in the query string. Accept it for hub paths only.
                        var accessToken = context.Request.Query["access_token"];

                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments(HubPathPrefix))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    private static IServiceCollection AddRealTimeNotifications(this IServiceCollection services)
    {
        services.AddSignalR(options =>
        {
            // Surface hub exceptions to staff clients in full only when developing; the default of
            // hiding them stays in force elsewhere.
            options.EnableDetailedErrors = false;
        });

        services.AddScoped<INotificationService, SignalRNotificationService>();

        return services;
    }

    /// <summary>
    /// Registers one authorization policy per row of the RBAC matrix.
    /// </summary>
    /// <remarks>
    /// Lives here beside the authentication setup so a new matrix row needs no startup change: the
    /// policies are derived from <see cref="AuthorizationPolicies"/>.
    /// </remarks>
    public static IServiceCollection AddRbacAuthorization(this IServiceCollection services)
    {
        var authorization = services.AddAuthorizationBuilder();

        foreach (var policyName in AuthorizationPolicies.All)
        {
            authorization.AddPolicy(policyName, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AuthorizationPolicies.RoleNamesFor(policyName)));
        }

        return services;
    }
}
