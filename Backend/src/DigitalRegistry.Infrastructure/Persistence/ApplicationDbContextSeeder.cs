using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DigitalRegistry.Infrastructure.Persistence;

/// <summary>
/// Brings a database up to a usable state: Identity roles always, demo content on request.
/// </summary>
/// <remarks>
/// The four roles are structural — every token carries one, so they must exist in any environment.
/// The demo content is separate and opt-in, because it creates accounts with known passwords and
/// must never appear in a deployed database.
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

    /// <summary>Applies any pending migrations.</summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
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
    /// Adds demo staff, tables, ingredients and a small menu. Safe to run repeatedly: each section
    /// is skipped when data is already present.
    /// </summary>
    public async Task SeedDemoDataAsync(CancellationToken cancellationToken = default)
    {
        logger.LogWarning(
            "Seeding demo data with well-known passwords. This must never run against a deployed database.");

        await SeedDemoUsersAsync();
        await SeedTablesAsync(cancellationToken);
        await SeedMenuAsync(cancellationToken);
    }

    private async Task SeedDemoUsersAsync()
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
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                Role = role
            };

            var createResult = await userManager.CreateAsync(user, DemoPassword);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create demo user '{email}': " +
                    string.Join("; ", createResult.Errors.Select(error => error.Description)));
            }

            await userManager.AddToRoleAsync(user, role.ToString());
            logger.LogInformation("Created demo {Role} account {Email}.", role, email);
        }
    }

    private async Task SeedTablesAsync(CancellationToken cancellationToken)
    {
        if (await context.Tables.AnyAsync(cancellationToken))
        {
            return;
        }

        int[] capacities = [2, 2, 4, 4, 4, 6, 6, 8];

        var tables = capacities
            .Select((capacity, index) => new Table
            {
                TableNumber = index + 1,
                Capacity = capacity,
                IsActive = true
            })
            .ToList();

        context.Tables.AddRange(tables);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} tables.", tables.Count);
    }

    private async Task SeedMenuAsync(CancellationToken cancellationToken)
    {
        if (await context.MenuItems.AnyAsync(cancellationToken))
        {
            return;
        }

        var ingredients = new Dictionary<string, Ingredient>
        {
            ["Espresso beans"] = new()
            {
                Name = "Espresso beans", StockQuantity = 5000m, Unit = UnitOfMeasure.Grams, LowStockThreshold = 500m
            },
            ["Milk"] = new()
            {
                Name = "Milk", StockQuantity = 20000m, Unit = UnitOfMeasure.Milliliters, LowStockThreshold = 2000m
            },
            ["Gin"] = new()
            {
                Name = "Gin", StockQuantity = 3000m, Unit = UnitOfMeasure.Milliliters, LowStockThreshold = 500m
            },
            ["Tonic water"] = new()
            {
                Name = "Tonic water", StockQuantity = 8000m, Unit = UnitOfMeasure.Milliliters, LowStockThreshold = 1000m
            },
            ["Lime"] = new()
            {
                Name = "Lime", StockQuantity = 40m, Unit = UnitOfMeasure.Units, LowStockThreshold = 10m
            },
            ["Burger patty"] = new()
            {
                Name = "Burger patty", StockQuantity = 60m, Unit = UnitOfMeasure.Units, LowStockThreshold = 12m
            },
            ["Burger bun"] = new()
            {
                Name = "Burger bun", StockQuantity = 60m, Unit = UnitOfMeasure.Units, LowStockThreshold = 12m
            },
            ["Cheddar"] = new()
            {
                Name = "Cheddar", StockQuantity = 2000m, Unit = UnitOfMeasure.Grams, LowStockThreshold = 300m
            }
        };

        context.Ingredients.AddRange(ingredients.Values);

        var menuItems = new List<MenuItem>
        {
            BuildMenuItem("Espresso", "Coffee", 2.20m, ingredients, ("Espresso beans", 18m)),
            BuildMenuItem("Cappuccino", "Coffee", 3.10m, ingredients, ("Espresso beans", 18m), ("Milk", 150m)),
            BuildMenuItem("Gin and Tonic", "Cocktails", 7.50m, ingredients,
                ("Gin", 50m), ("Tonic water", 150m), ("Lime", 0.25m)),
            BuildMenuItem("Cheeseburger", "Food", 11.90m, ingredients,
                ("Burger patty", 1m), ("Burger bun", 1m), ("Cheddar", 30m))
        };

        context.MenuItems.AddRange(menuItems);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded {IngredientCount} ingredients and {MenuItemCount} menu items.",
            ingredients.Count,
            menuItems.Count);
    }

    private static MenuItem BuildMenuItem(
        string name,
        string category,
        decimal unitPrice,
        IReadOnlyDictionary<string, Ingredient> ingredients,
        params (string IngredientName, decimal Quantity)[] recipe)
    {
        var menuItem = new MenuItem
        {
            Name = name,
            Category = category,
            UnitPrice = unitPrice,
            IsAvailable = true
        };

        foreach (var (ingredientName, quantity) in recipe)
        {
            menuItem.Recipe.Add(new RecipeItem
            {
                Ingredient = ingredients[ingredientName],
                QuantityRequired = quantity
            });
        }

        return menuItem;
    }
}
