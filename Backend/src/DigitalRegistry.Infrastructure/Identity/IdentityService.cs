using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DigitalRegistry.Infrastructure.Identity;

/// <summary>
/// Implements account operations over ASP.NET Core Identity and issues tokens.
/// </summary>
/// <remarks>
/// Accounts are addressed by restaurant slug plus email; see <see cref="TenantUserName"/> for why the
/// two are composed into Identity's user name rather than the email being used on its own.
/// </remarks>
internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenGenerator tokenGenerator,
    IDigitalRegistryDbContext dbContext,
    ILogger<IdentityService> logger) : IIdentityService
{
    /// <summary>
    /// Returned for an unknown restaurant, an unknown email and a wrong password alike, so the
    /// response cannot be used to enumerate which venues or addresses are registered.
    /// </summary>
    private const string InvalidCredentialsMessage = "Invalid restaurant code, email or password.";

    /// <summary>
    /// The master API's equivalent. Says nothing about the account's role, so the endpoint cannot be
    /// used to discover which addresses are platform administrators.
    /// </summary>
    private const string InvalidAdminCredentialsMessage = "Invalid email or password.";

    public async Task<Result<AuthenticationResult>> LoginAsync(
        string restaurantSlug,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await FindRestaurantAsync(restaurantSlug, cancellationToken);

        if (restaurant is null)
        {
            logger.LogInformation("Login attempt against an unknown or inactive restaurant.");
            return Result<AuthenticationResult>.Unauthorized(InvalidCredentialsMessage);
        }

        var user = await userManager.FindByNameAsync(TenantUserName.For(restaurant.Slug, email));
        
        if (user is null)
        {
            logger.LogInformation("Login attempt for an unregistered address at restaurant {RestaurantId}.", restaurant.Id);
            return Result<AuthenticationResult>.Unauthorized(InvalidCredentialsMessage);
        }

        // lockoutOnFailure enables Identity's built-in throttling of repeated wrong passwords.
        var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            logger.LogWarning("Login blocked for user {UserId}: account is locked out.", user.Id);
            return Result<AuthenticationResult>.Forbidden(
                "This account is temporarily locked after too many failed attempts. Try again later.");
        }

        if (!signInResult.Succeeded)
        {
            logger.LogInformation("Failed login for user {UserId}.", user.Id);
            return Result<AuthenticationResult>.Unauthorized(InvalidCredentialsMessage);
        }

        return Result<AuthenticationResult>.Success(BuildUserAuthentication(user, restaurant.Slug));
    }

    public async Task<Result<AuthenticationResult>> RegisterGuestAsync(
        string restaurantSlug,
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await FindRestaurantAsync(restaurantSlug, cancellationToken);

        if (restaurant is null)
        {
            return Result<AuthenticationResult>.NotFound("No restaurant is registered under that code.");
        }

        var userName = TenantUserName.For(restaurant.Slug, email);

        if (await userManager.FindByNameAsync(userName) is not null)
        {
            return Result<AuthenticationResult>.Conflict(
                "An account with this email address already exists at this restaurant.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            RestaurantId = restaurant.Id,
            // Self-registration always produces a guest; staff roles are provisioned separately.
            Role = UserRole.Guest
        };

        var createResult = await userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            return Result<AuthenticationResult>.Invalid(
                createResult.Errors.Select(error => error.Description).ToArray());
        }

        var roleResult = await userManager.AddToRoleAsync(user, UserRole.Guest.ToString());

        if (!roleResult.Succeeded)
        {
            // The account exists but carries no role, which would leave it unable to do anything.
            // Roll it back rather than leave a half-provisioned user behind.
            await userManager.DeleteAsync(user);

            logger.LogError(
                "Role assignment failed while registering a guest: {Errors}",
                string.Join("; ", roleResult.Errors.Select(error => error.Description)));

            return Result<AuthenticationResult>.Invalid("Registration could not be completed. Please try again.");
        }

        logger.LogInformation("Registered guest {UserId} at restaurant {RestaurantId}.", user.Id, restaurant.Id);

        return Result<AuthenticationResult>.Success(BuildUserAuthentication(user, restaurant.Slug));
    }

    public Result<AuthenticationResult> IssueTableSessionToken(Guid restaurantId, Guid tableId, int tableNumber)
    {
        var (token, expiresAtUtc) = tokenGenerator.GenerateForTableSession(restaurantId, tableId, tableNumber);

        return Result<AuthenticationResult>.Success(new AuthenticationResult(
            AccessToken: token,
            ExpiresAtUtc: expiresAtUtc,
            UserId: null,
            Email: null,
            FullName: null,
            Role: UserRole.Guest,
            RestaurantId: restaurantId,
            TableId: tableId,
            TableNumber: tableNumber));
    }

    public async Task<Result<Guid>> CreateAccountAsync(
        Guid restaurantId,
        string restaurantSlug,
        string email,
        string password,
        string firstName,
        string lastName,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        if (role == UserRole.PlatformAdmin)
        {
            // Platform administrators belong to no restaurant, so provisioning one through the
            // per-restaurant path would produce an account that is neither one thing nor the other.
            return Result<Guid>.Invalid("A platform administrator cannot be created against a restaurant.");
        }

        var userName = TenantUserName.For(restaurantSlug, email);

        if (await userManager.FindByNameAsync(userName) is not null)
        {
            return Result<Guid>.Conflict("An account with this email address already exists at this restaurant.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            RestaurantId = restaurantId,
            Role = role
        };

        var createResult = await userManager.CreateAsync(user, password);

        if (!createResult.Succeeded)
        {
            return Result<Guid>.Invalid(createResult.Errors.Select(error => error.Description).ToArray());
        }

        var roleResult = await userManager.AddToRoleAsync(user, role.ToString());

        if (!roleResult.Succeeded)
        {
            // Same reasoning as guest registration: an account with no role can do nothing, so it is
            // rolled back rather than left half-provisioned.
            await userManager.DeleteAsync(user);

            logger.LogError(
                "Role assignment failed while provisioning {Role} at restaurant {RestaurantId}: {Errors}",
                role,
                restaurantId,
                string.Join("; ", roleResult.Errors.Select(error => error.Description)));

            return Result<Guid>.Invalid("The account could not be created. Please try again.");
        }

        logger.LogInformation("Provisioned {Role} account {UserName}.", role, userName);

        return Result<Guid>.Success(user.Id);
    }

    public async Task<Result<AuthenticationResult>> LoginPlatformAdminAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Platform administrators carry no slug prefix, so the plain email is the user name. No
        // restaurant slug can be empty, which is what keeps the two namespaces from colliding.
        var user = await userManager.FindByNameAsync(email.Trim());

        if (user is null || user.Role != UserRole.PlatformAdmin)
        {
            logger.LogInformation("Platform administrator login attempt for a non-administrator account.");
            return Result<AuthenticationResult>.Unauthorized(InvalidAdminCredentialsMessage);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (signInResult.IsLockedOut)
        {
            logger.LogWarning("Platform administrator {UserId} is locked out.", user.Id);
            return Result<AuthenticationResult>.Forbidden(
                "This account is temporarily locked after too many failed attempts. Try again later.");
        }

        if (!signInResult.Succeeded)
        {
            logger.LogInformation("Failed platform administrator login for user {UserId}.", user.Id);
            return Result<AuthenticationResult>.Unauthorized(InvalidAdminCredentialsMessage);
        }

        return Result<AuthenticationResult>.Success(BuildUserAuthentication(user, restaurantSlug: null));
    }

    public async Task<Result> SetAccountEnabledAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.NotFound("No such account.");
        }

        if (user.Role == UserRole.Owner && !enabled)
        {
            // Locking out the owner would leave the venue with nobody able to manage it, and nobody
            // able to undo the lockout either.
            return Result.Conflict("The owner's account cannot be switched off.");
        }

        // Lockout has to be enabled on the account for an end date to mean anything.
        await userManager.SetLockoutEnabledAsync(user, true);

        var result = await userManager.SetLockoutEndDateAsync(
            user,
            enabled ? null : DateTimeOffset.MaxValue);

        if (!result.Succeeded)
        {
            return Result.Invalid(result.Errors.Select(error => error.Description).ToArray());
        }

        logger.LogInformation("Account {UserId} was {State}.", userId, enabled ? "enabled" : "disabled");

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.NotFound("No such account.");
        }

        // Generating a token and immediately spending it is Identity's supported way to set a
        // password without knowing the current one.
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);

        if (!result.Succeeded)
        {
            return Result.Invalid(result.Errors.Select(error => error.Description).ToArray());
        }

        logger.LogInformation("Password reset for account {UserId}.", userId);

        return Result.Success();
    }

    public async Task<bool> IsInRoleAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        // Read the role column directly: it is the authoritative value the domain rules use, and this
        // avoids loading the whole user just to compare one field.
        return await userManager.Users
            .Where(user => user.Id == userId)
            .Select(user => user.Role)
            .FirstOrDefaultAsync(cancellationToken) == role;
    }

    /// <summary>
    /// Resolves a sign-in code to an active restaurant.
    /// </summary>
    /// <remarks>
    /// A suspended venue is treated as if it did not exist, which stops its staff from obtaining a
    /// token at all rather than letting them in and refusing every subsequent call.
    /// </remarks>
    private async Task<Restaurant?> FindRestaurantAsync(string restaurantSlug, CancellationToken cancellationToken)
    {
        var slug = TenantUserName.NormalizeSlug(restaurantSlug);

        return await dbContext.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(restaurant => restaurant.Slug == slug && restaurant.IsActive, cancellationToken);
    }

    private AuthenticationResult BuildUserAuthentication(ApplicationUser user, string? restaurantSlug)
    {
        var (token, expiresAtUtc) = tokenGenerator.GenerateForUser(user, restaurantSlug);

        return new AuthenticationResult(
            AccessToken: token,
            ExpiresAtUtc: expiresAtUtc,
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role,
            RestaurantId: user.RestaurantId,
            RestaurantSlug: restaurantSlug);
    }
}
