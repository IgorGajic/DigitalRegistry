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
    /// Verifies credentials and issues an access token.
    /// </summary>
    /// <remarks>
    /// Failure is deliberately reported as a single undifferentiated error so the response cannot be
    /// used to discover which email addresses are registered.
    /// </remarks>
    Task<Result<AuthenticationResult>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a self-registered guest account and signs them straight in.</summary>
    Task<Result<AuthenticationResult>> RegisterGuestAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mints an anonymous, table-scoped token for a guest who scanned a table's QR code.
    /// </summary>
    /// <remarks>
    /// The token carries the table id and the <see cref="UserRole.Guest"/> role but no user id: it
    /// permits viewing the menu and ordering for that one table, nothing else.
    /// </remarks>
    Result<AuthenticationResult> IssueTableSessionToken(Guid tableId, int tableNumber);

    /// <summary>True when the given user holds the given role.</summary>
    Task<bool> IsInRoleAsync(Guid userId, UserRole role, CancellationToken cancellationToken = default);
}
