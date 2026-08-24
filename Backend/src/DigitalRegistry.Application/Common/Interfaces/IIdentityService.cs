using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Common.Interfaces;

/// <summary>
/// User account operations and token issuance, implemented over ASP.NET Core Identity.
/// </summary>
/// <remarks>
/// Keeps <c>UserManager</c>, <c>SignInManager</c> and JWT construction out of the Application layer,
/// so handlers stay free of any particular identity provider.
/// </remarks>
public interface IIdentityService
{
    /// <summary>
    /// Verifies credentials against one restaurant and issues an access token.
    /// </summary>
    /// <param name="restaurantSlug">
    /// The venue's sign-in code. Required because an email address identifies an account only within
    /// a restaurant, never across the platform.
    /// </param>
    /// <remarks>
    /// Failure is deliberately reported as a single undifferentiated error — covering an unknown
    /// restaurant, an unknown email and a wrong password alike — so the response cannot be used to
    /// discover which venues or addresses are registered.
    /// </remarks>
    Task<Result<AuthenticationResult>> LoginAsync(
        string restaurantSlug,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a self-registered guest account at one restaurant and signs them straight in.</summary>
    Task<Result<AuthenticationResult>> RegisterGuestAsync(
        string restaurantSlug,
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mints an anonymous, table-scoped token for a guest who scanned a table's QR code.
    /// </summary>
    /// <remarks>
    /// The token carries the restaurant, the table id and the <see cref="UserRole.Guest"/> role but no
    /// user id: it permits viewing the menu and ordering for that one table, nothing else. The
    /// restaurant claim is what keeps such a session inside the global query filters.
    /// </remarks>
    Result<AuthenticationResult> IssueTableSessionToken(Guid restaurantId, Guid tableId, int tableNumber);

    /// <summary>
    /// Provisions a staff account at a restaurant.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="RegisterGuestAsync"/> because the role is chosen by the caller rather
    /// than fixed: this is how a platform administrator creates an owner, and how an owner later
    /// creates waiters and managers. Authorization for that decision belongs to the handler, not here.
    /// </remarks>
    Task<Result<Guid>> CreateAccountAsync(
        Guid restaurantId,
        string restaurantSlug,
        string email,
        string password,
        string firstName,
        string lastName,
        UserRole role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a platform administrator's credentials and issues a token for the master API.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="LoginAsync"/> because these accounts belong to no restaurant: there
    /// is no slug to give, their user name is the plain email, and the token they receive carries no
    /// restaurant claim. Anyone whose stored role is not
    /// <see cref="UserRole.PlatformAdmin"/> is refused here even with the right password, so a
    /// restaurant owner cannot reach the master API by pointing at a different endpoint.
    /// </remarks>
    Task<Result<AuthenticationResult>> LoginPlatformAdminAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches an account off, or back on, without deleting it.
    /// </summary>
    /// <remarks>
    /// Implemented as an Identity lockout with no end date rather than a flag of our own. A waiter who
    /// leaves must stop being able to sign in, but their name has to stay on every order and shift
    /// they worked — deleting the account would take the history with it.
    /// </remarks>
    Task<Result> SetAccountEnabledAsync(
        Guid userId,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a new password for an account.
    /// </summary>
    /// <remarks>
    /// For an owner resetting a password on behalf of staff who have forgotten theirs, so it takes no
    /// current password. There is no self-service reset: a till has no email delivery behind it.
    /// </remarks>
    Task<Result> ResetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>True when the given user holds the given role.</summary>
    Task<bool> IsInRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);
}
