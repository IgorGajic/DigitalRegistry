using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DigitalRegistry.Infrastructure.Identity;

/// <summary>
/// Implements account operations over ASP.NET Core Identity and issues tokens.
/// </summary>
internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenGenerator tokenGenerator,
    ILogger<IdentityService> logger) : IIdentityService
{
    /// <summary>
    /// Returned for both an unknown email and a wrong password, so the response cannot be used to
    /// enumerate which addresses are registered.
    /// </summary>
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<Result<AuthenticationResult>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            logger.LogInformation("Login attempt for an unregistered address.");
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

        return Result<AuthenticationResult>.Success(BuildUserAuthentication(user));
    }

    public async Task<Result<AuthenticationResult>> RegisterGuestAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return Result<AuthenticationResult>.Conflict("An account with this email address already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
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

        logger.LogInformation("Registered guest {UserId}.", user.Id);

        return Result<AuthenticationResult>.Success(BuildUserAuthentication(user));
    }

    public Result<AuthenticationResult> IssueTableSessionToken(Guid tableId, int tableNumber)
    {
        var (token, expiresAtUtc) = tokenGenerator.GenerateForTableSession(tableId, tableNumber);

        return Result<AuthenticationResult>.Success(new AuthenticationResult(
            AccessToken: token,
            ExpiresAtUtc: expiresAtUtc,
            UserId: null,
            Email: null,
            FullName: null,
            Role: UserRole.Guest,
            TableId: tableId));
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

    private AuthenticationResult BuildUserAuthentication(ApplicationUser user)
    {
        var (token, expiresAtUtc) = tokenGenerator.GenerateForUser(user);

        return new AuthenticationResult(
            AccessToken: token,
            ExpiresAtUtc: expiresAtUtc,
            UserId: user.Id,
            Email: user.Email,
            FullName: user.FullName,
            Role: user.Role);
    }
}
