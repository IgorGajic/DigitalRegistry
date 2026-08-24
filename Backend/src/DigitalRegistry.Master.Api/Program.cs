using System.Reflection;
using DigitalRegistry.Api.Shared.Middleware;
using DigitalRegistry.Api.Shared.Serialization;
using DigitalRegistry.Api.Shared.Services;
using DigitalRegistry.Application;
using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Infrastructure;
using DigitalRegistry.Infrastructure.Persistence;
using DigitalRegistry.Master.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------------------------

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// The one policy this host has. Every endpoint sits behind it, so a restaurant's token — which
// carries Owner or Manager, never PlatformAdmin — is refused even though it is perfectly valid at
// the till. The differing audience in configuration means such a token is normally rejected before
// it gets this far; the policy is the second lock.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PlatformAuthorization.PlatformAdminOnly, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(UserRole.PlatformAdmin.ToString()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// The master application deliberately belongs to no restaurant: it reads across all of them by
// bypassing the query filters in its handlers, which is why that bypass has to be written out
// explicitly wherever it happens.
builder.Services.AddScoped<ITenantContext>(_ => NullTenantContext.Instance);

builder.Services.AddControllers()
    // Timestamps come back from SQL Server with no kind, which would serialise without the trailing
    // Z and be read by a browser as local time. See UtcDateTimeJsonConverter.
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressMapClientErrors = false;
});

builder.Services.AddProblemDetails();

const string CorsPolicyName = "DigitalRegistryMasterClients";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            return;
        }

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    }));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DigitalRegistry Master API",
        Version = "v1",
        Description = "Platform administration: restaurants, licences and licence payments."
    });

    const string BearerSchemeId = "Bearer";

    options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by /api/platform/auth/login."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(BearerSchemeId, document, null)] = new List<string>()
    });

    var xmlDocumentation = Path.Combine(
        AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");

    if (File.Exists(xmlDocumentation))
    {
        options.IncludeXmlComments(xmlDocumentation);
    }
});

var app = builder.Build();

// ---------------------------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------------------------

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DigitalRegistry Master API v1");
        options.DocumentTitle = "DigitalRegistry Master API";
    });

    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}
else
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseCors(CorsPolicyName);

app.UseAuthentication();
app.UseAuthorization();

// No licence guard here, and none wanted: this is the host that sells the licences.

app.MapControllers();

// ---------------------------------------------------------------------------------------------
// Database initialisation
// ---------------------------------------------------------------------------------------------

// Migrations belong to the till's host, which owns the schema. This one only ensures the platform
// administrator account exists, so the master application is reachable on a fresh database.
await EnsurePlatformAdminAsync(app);

await app.RunAsync();

static async Task EnsurePlatformAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<ApplicationDbContextSeeder>();

    await seeder.SeedRolesAsync();
    await seeder.SeedPlatformAdminAsync(app.Configuration);
}

/// <summary>
/// Declared so integration tests can reference this host through
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
