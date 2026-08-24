using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DigitalRegistry.Infrastructure.Persistence;

/// <summary>
/// Brings a database up to a usable state: Identity roles always, demo content on request.
/// </summary>
/// <remarks>
/// The roles are structural — every token carries one, so they must exist in any environment. The
/// demo content is separate and opt-in, because it creates accounts with known passwords and must
/// never appear in a deployed database.
/// <para>
/// This runs at startup, outside any request, so there is no ambient tenant. Every read below
/// therefore calls <c>IgnoreQueryFilters()</c> — otherwise the "is this already seeded?" checks would
/// see an empty database and duplicate everything — and every write sets the restaurant explicitly
/// rather than relying on the DbContext to stamp it.
/// </para>
/// </remarks>
public class ApplicationDbContextSeeder(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    ILogger<ApplicationDbContextSeeder> logger)
{
    /// <summary>
    /// Password given to every seeded demo account. Development only; these accounts exist purely so
    /// the API can be exercised without a provisioning UI.
    /// </summary>
    private const string DemoPassword = "Demo#Pass123";

    /// <summary>Sign-in code for the demo restaurant, typed alongside the email at login.</summary>
    private const string DemoRestaurantSlug = "demo";

    /// <summary>Applies any pending migrations.</summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        // The integration tests run the host against the in-memory provider, which has no migrations
        // and throws when asked for them. The model it builds comes from the same configuration, so
        // there is nothing to apply and nothing to check.
        if (!context.Database.IsRelational())
        {
            return;
        }

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count == 0)
        {
            return;
        }

        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Count, pending);
        await context.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>Creates the Identity role per <see cref="UserRole"/> value if missing.</summary>
    public async Task SeedRolesAsync()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            var roleName = role.ToString();

            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName)
            {
                Id = Guid.NewGuid(),
                NormalizedName = roleName.ToUpperInvariant()
            });

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create role '{roleName}': " +
                    string.Join("; ", result.Errors.Select(error => error.Description)));
            }

            logger.LogInformation("Created role {RoleName}.", roleName);
        }
    }

    /// <summary>
    /// Creates the platform administrator account from configuration if it does not exist.
    /// </summary>
    /// <remarks>
    /// Run by the master application at startup, because a fresh database would otherwise have nobody
    /// able to sign in to it and no way to create anybody. Unlike the demo data this is not gated on
    /// the environment — a deployment needs a first administrator too — so the credentials come from
    /// configuration rather than being hard-coded, and the seeder does nothing at all when they are
    /// absent.
    /// <para>
    /// Only ever creates. An existing administrator's password is never reset from configuration,
    /// so a stale setting cannot quietly hand out access to a live account.
    /// </para>
    /// </remarks>
    public async Task SeedPlatformAdminAsync(IConfiguration configuration)
    {
        var email = configuration["PlatformAdmin:Email"];
        var password = configuration["PlatformAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation(
                "No PlatformAdmin credentials are configured; skipping administrator seeding.");
            return;
        }

        // Platform administrators carry no restaurant slug, so their user name is the plain email.
        if (await userManager.FindByNameAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = configuration["PlatformAdmin:FirstName"] ?? "Platform",
            LastName = configuration["PlatformAdmin:LastName"] ?? "Administrator",
            RestaurantId = null,
            Role = UserRole.PlatformAdmin
        };

        var createResult = await userManager.CreateAsync(admin, password);

        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the platform administrator '{email}': " +
                string.Join("; ", createResult.Errors.Select(error => error.Description)));
        }

        await userManager.AddToRoleAsync(admin, UserRole.PlatformAdmin.ToString());

        logger.LogWarning(
            "Created the platform administrator {Email} from configuration. Change its password.",
            email);
    }

    /// <summary>
    /// Adds demo staff, tables, ingredients and a small menu. Safe to run repeatedly: each section
    /// is skipped when data is already present.
    /// </summary>
    public async Task SeedDemoDataAsync(CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Seeding demo data with well-known passwords. This must never run against a deployed database.");

        var restaurant = await SeedDemoRestaurantAsync(cancellationToken);

        await SeedDemoUsersAsync(restaurant);
        await SeedDemoLicenseAsync(restaurant, cancellationToken);
        await SeedTablesAsync(restaurant, cancellationToken);
        await SeedMenuAsync(restaurant, cancellationToken);
        await SeedShiftTemplatesAsync(restaurant, cancellationToken);
    }

    /// <summary>
    /// Adds the two shifts a venue of this kind runs, so the rota screen opens on something usable.
    /// </summary>
    /// <remarks>
    /// Templates only. No assignments and no generated shifts: who works when is the manager's to
    /// decide, and inventing a rota for the demo staff would only be in the way.
    /// </remarks>
    private async Task SeedShiftTemplatesAsync(Restaurant restaurant, CancellationToken cancellationToken)
    {
        if (await context.ShiftTemplates
                .IgnoreQueryFilters()
                .AnyAsync(template => template.RestaurantId == restaurant.Id, cancellationToken))
        {
            return;
        }

        context.ShiftTemplates.AddRange(
            new ShiftTemplate
            {
                RestaurantId = restaurant.Id,
                Name = "I smena",
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(15, 0)
            },
            new ShiftTemplate
            {
                RestaurantId = restaurant.Id,
                Name = "II smena",
                StartTime = new TimeOnly(15, 0),
                EndTime = new TimeOnly(23, 0)
            });

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded 2 shift templates.");
    }

    /// <summary>
    /// Gives the demo restaurant a licence so the till is actually usable.
    /// </summary>
    /// <remarks>
    /// Without one the licence guard would refuse every call and the demo data would be inert. A year
    /// is chosen so nobody returning to the project after a few months finds it locked.
    /// </remarks>
    private async Task SeedDemoLicenseAsync(Restaurant restaurant, CancellationToken cancellationToken)
    {
        if (await context.Licenses
                .IgnoreQueryFilters()
                .AnyAsync(license => license.RestaurantId == restaurant.Id, cancellationToken))
        {
            return;
        }

        // No administrator has issued this one; it exists so the demo works.
        var license = License.Issue(
            restaurant.Id,
            LicensePlan.Annual,
            price: 0m,
            issuedByAdminId: Guid.Empty,
            utcNow: DateTime.UtcNow,
            notes: "Seeded demo licence.");

        context.Licenses.Add(license);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded a demo licence expiring {ExpiresAtUtc:u}.", license.ExpiresAtUtc);
    }

    /// <summary>Creates the demo tenant everything else in the demo data hangs off.</summary>
    private async Task<Restaurant> SeedDemoRestaurantAsync(CancellationToken cancellationToken)
    {
        var existing = await context.Restaurants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Slug == DemoRestaurantSlug, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var restaurant = new Restaurant
        {
            Id = Guid.NewGuid(),
            Name = "Demo restoran",
            Slug = DemoRestaurantSlug,
            Address = "Knez Mihailova 1, Beograd",
            ContactEmail = "kontakt@digitalregistry.local",
            PhoneNumber = "+381 11 000 000",
            CurrencyCode = "RSD",
            TimeZoneId = "Europe/Belgrade",
            IsActive = true
        };

        context.Restaurants.Add(restaurant);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded demo restaurant {Slug} ({RestaurantId}).", restaurant.Slug, restaurant.Id);

        return restaurant;
    }

    private async Task SeedDemoUsersAsync(Restaurant restaurant)
    {
        (string Email, string First, string Last, UserRole Role)[] demoUsers =
        [
            ("owner@digitalregistry.local", "Olivia", "Owner", UserRole.Owner),
            ("manager@digitalregistry.local", "Marko", "Manager", UserRole.Manager),
            ("waiter@digitalregistry.local", "Wren", "Waiter", UserRole.Waiter),
            ("waiter2@digitalregistry.local", "Wesley", "Waiter", UserRole.Waiter),
            ("guest@digitalregistry.local", "Greta", "Guest", UserRole.Guest)
        ];

        foreach (var (email, firstName, lastName, role) in demoUsers)
        {
            var userName = TenantUserName.For(restaurant.Slug, email);

            // Looked up by user name, not email: emails are no longer unique across the platform.
            if (await userManager.FindByNameAsync(userName) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                RestaurantId = restaurant.Id,
                Role = role
            };

            var createResult = await userManager.CreateAsync(user, DemoPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create demo user '{userName}': " +
                    string.Join("; ", createResult.Errors.Select(error => error.Description)));
            }

            await userManager.AddToRoleAsync(user, role.ToString());
            logger.LogInformation("Created demo {Role} account {UserName}.", role, userName);
        }
    }

    private async Task SeedTablesAsync(Restaurant restaurant, CancellationToken cancellationToken)
    {
        if (await context.Tables
                .IgnoreQueryFilters()
                .AnyAsync(table => table.RestaurantId == restaurant.Id, cancellationToken))
        {
            return;
        }

        // Two rooms, so the floor screen has more than one tab to show and the layout editor has
        // somewhere to drag a table to.
        var mainRoom = new Room
        {
            RestaurantId = restaurant.Id,
            Name = "Sala",
            DisplayOrder = 0
        };

        var terrace = new Room
        {
            RestaurantId = restaurant.Id,
            Name = "Bašta",
            DisplayOrder = 1,
            CanvasWidth = 900,
            CanvasHeight = 600
        };

        context.Rooms.AddRange(mainRoom, terrace);

        // Laid out in two rows of three inside, and a row of two on the terrace, so the demo opens on
        // a floor plan that already reads like a room rather than a pile of tables at the origin.
        (int Capacity, Room Room, int X, int Y, TableShape Shape, int Width, int Height)[] layout =
        [
            (2, mainRoom, 120, 120, TableShape.Round, 80, 80),
            (2, mainRoom, 360, 120, TableShape.Round, 80, 80),
            (4, mainRoom, 600, 120, TableShape.Square, 100, 100),
            (4, mainRoom, 120, 360, TableShape.Square, 100, 100),
            (4, mainRoom, 360, 360, TableShape.Square, 100, 100),
            (6, mainRoom, 600, 360, TableShape.Rectangle, 180, 100),
            (6, terrace, 120, 150, TableShape.Rectangle, 180, 100),
            (8, terrace, 450, 150, TableShape.Rectangle, 220, 100)
        ];

        var tables = layout
            .Select((entry, index) => new Table
            {
                RestaurantId = restaurant.Id,
                TableNumber = index + 1,
                Capacity = entry.Capacity,
                IsActive = true,
                Room = entry.Room,
                PositionX = entry.X,
                PositionY = entry.Y,
                Width = entry.Width,
                Height = entry.Height,
                Shape = entry.Shape
            })
            .ToList();

        context.Tables.AddRange(tables);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded 2 rooms and {Count} tables.", tables.Count);
    }

    private async Task SeedMenuAsync(Restaurant restaurant, CancellationToken cancellationToken)
    {
        if (await context.MenuItems
                .IgnoreQueryFilters()
                .AnyAsync(menuItem => menuItem.RestaurantId == restaurant.Id, cancellationToken))
        {
            return;
        }

        var ingredients = new Dictionary<string, Ingredient>
        {
            // Purchase prices are per unit in RSD, so the store has a value and menu margins can be
            // computed from the first run rather than reading as pure profit until a delivery arrives.
            ["Espresso beans"] = NewIngredient(restaurant, "Espresso beans", 5000m, UnitOfMeasure.Grams, 500m, 1.80m),
            ["Milk"] = NewIngredient(restaurant, "Milk", 20000m, UnitOfMeasure.Milliliters, 2000m, 0.16m),
            ["Gin"] = NewIngredient(restaurant, "Gin", 3000m, UnitOfMeasure.Milliliters, 500m, 2.40m),
            ["Tonic water"] = NewIngredient(restaurant, "Tonic water", 8000m, UnitOfMeasure.Milliliters, 1000m, 0.35m),
            ["Lime"] = NewIngredient(restaurant, "Lime", 40m, UnitOfMeasure.Units, 10m, 45m),
            ["Burger patty"] = NewIngredient(restaurant, "Burger patty", 60m, UnitOfMeasure.Units, 12m, 210m),
            ["Burger bun"] = NewIngredient(restaurant, "Burger bun", 60m, UnitOfMeasure.Units, 12m, 35m),
            ["Cheddar"] = NewIngredient(restaurant, "Cheddar", 2000m, UnitOfMeasure.Grams, 300m, 1.10m)
        };

        context.Ingredients.AddRange(ingredients.Values);

        // Prices are in RSD, the demo restaurant's currency.
        var menuItems = new List<MenuItem>
        {
            BuildMenuItem(restaurant, "Espresso", "Coffee", 180m, ingredients, ("Espresso beans", 18m)),
            BuildMenuItem(restaurant, "Cappuccino", "Coffee", 250m, ingredients,
                ("Espresso beans", 18m), ("Milk", 150m)),
            BuildMenuItem(restaurant, "Gin and Tonic", "Cocktails", 650m, ingredients,
                ("Gin", 50m), ("Tonic water", 150m), ("Lime", 0.25m)),
            BuildMenuItem(restaurant, "Cheeseburger", "Food", 890m, ingredients,
                ("Burger patty", 1m), ("Burger bun", 1m), ("Cheddar", 30m))
        };

        context.MenuItems.AddRange(menuItems);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {IngredientCount} ingredients and {MenuItemCount} menu items.",
            ingredients.Count,
            menuItems.Count);
    }

    private static Ingredient NewIngredient(
        Restaurant restaurant,
        string name,
        decimal stockQuantity,
        UnitOfMeasure unit,
        decimal lowStockThreshold,
        decimal averagePurchasePrice) => new()
        {
            RestaurantId = restaurant.Id,
            Name = name,
            StockQuantity = stockQuantity,
            Unit = unit,
            LowStockThreshold = lowStockThreshold,
            AveragePurchasePrice = averagePurchasePrice
        };

    private static MenuItem BuildMenuItem(
        Restaurant restaurant,
        string name,
        string category,
        decimal unitPrice,
        IReadOnlyDictionary<string, Ingredient> ingredients,
        params (string IngredientName, decimal Quantity)[] recipe)
    {
        var menuItem = new MenuItem
        {
            RestaurantId = restaurant.Id,
            Name = name,
            Category = category,
            UnitPrice = unitPrice,
            IsAvailable = true
        };

        foreach (var (ingredientName, quantity) in recipe)
        {
            menuItem.Recipe.Add(new RecipeItem
            {
                RestaurantId = restaurant.Id,
                Ingredient = ingredients[ingredientName],
                QuantityRequired = quantity
            });
        }

        return menuItem;
    }
}
