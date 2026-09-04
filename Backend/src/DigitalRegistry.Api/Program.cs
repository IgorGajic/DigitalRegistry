using System.Reflection;
// The licence guard is the till's alone: the master application is not subject to any one venue's
// licence, so it stays here rather than in the shared library.
using DigitalRegistry.Api.Middleware;
using DigitalRegistry.Api.Shared.Middleware;
using DigitalRegistry.Api.Shared.Serialization;
using DigitalRegistry.Api.Shared.Services;
using DigitalRegistry.Application;
using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Infrastructure;
using DigitalRegistry.Infrastructure.Persistence;
using DigitalRegistry.Infrastructure.RealTime.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------------------------

// Each layer registers its own dependencies, so this file states composition rather than detail.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRbacAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
// Both read the current request's claims, which is why they live in this layer rather than in
// Infrastructure. The DbContext takes the tenant from here to filter and stamp every row.
builder.Services.AddScoped<ITenantContext, TenantContext>();

builder.Services.AddControllers()
    // Timestamps come back from SQL Server with no kind, which would serialise without the trailing
    // Z and be read by a browser as local time. See UtcDateTimeJsonConverter.
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    });

// Model-binding failures should look like every other validation failure the API returns.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressMapClientErrors = false;
});

builder.Services.AddProblemDetails();

const string CorsPolicyName = "DigitalRegistryClients";
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
            .AllowAnyMethod()
            // Cross-origin JavaScript sees only a handful of response headers unless they are named
            // here. Without this the report export saves under a generic fallback name instead of
            // the one the server chose, which carries the period the report covers.
            .WithExposedHeaders("Content-Disposition")
            // Required for the SignalR browser client, which sends credentials on the handshake.
            .AllowCredentials();
    }));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DigitalRegistry API",
        Version = "v1",
        Description = "Bar and restaurant management: reservations, QR self-ordering, orders, "
                      + "payments, shift scheduling and inventory."
    });

    // Microsoft.OpenApi 2.x separates a scheme's definition from references to it, so the
    // definition is registered by name and the requirement points at that name.
    const string BearerSchemeId = "Bearer";

    options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by /api/auth/login. Swagger adds the "
                      + "\"Bearer\" prefix itself."
    });

    // The requirement is built per document so the scheme reference resolves against that
    // document's components rather than dangling.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(BearerSchemeId, document, null)] = new List<string>()
    });

    // Surfaces the XML doc comments on controllers and commands in the generated document.
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

// First in the chain so it also catches failures thrown by the middleware below it.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DigitalRegistry API v1");
        options.DocumentTitle = "DigitalRegistry API";
    });

    // Land on the docs when the app is opened at its root (e.g. started outside launchSettings).
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}
else
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseCors(CorsPolicyName);

app.UseAuthentication();

// After authentication, because the restaurant it checks comes from the validated token; before
// authorization, so an unlicensed venue is told to pay rather than told it lacks a role.
app.UseMiddleware<LicenseGuardMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.MapHub<KitchenHub>("/hubs/kitchen");
app.MapHub<OrderHub>("/hubs/order");

// ---------------------------------------------------------------------------------------------
// Database initialisation
// ---------------------------------------------------------------------------------------------

await InitialiseDatabaseAsync(app);

await app.RunAsync();

// Applies migrations, ensures the Identity roles exist, and seeds demo data when configured.
// Migrating on startup suits a single-instance deployment. Demo seeding is gated on both the
// SeedDemoData setting and the environment, because it creates accounts with known passwords.
static async Task InitialiseDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<ApplicationDbContextSeeder>();

    await seeder.MigrateAsync();
    await seeder.SeedRolesAsync();

    if (app.Environment.IsDevelopment() && app.Configuration.GetValue("SeedDemoData", false))
    {
        await seeder.SeedDemoDataAsync();
    }
}

/// <summary>
/// Declared so the integration tests can reference the host through
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
