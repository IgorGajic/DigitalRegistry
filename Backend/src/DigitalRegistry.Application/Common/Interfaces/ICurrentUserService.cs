using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Common.Interfaces;

/// <summary>
/// The identity behind the request currently being handled.
/// </summary>
/// <remarks>
/// Handlers read the caller from here instead of accepting a user id on the command, so a client
/// cannot act as somebody else by putting a different id in the request body.
/// </remarks>
public interface ICurrentUserService
{
    /// <summary>The signed-in user's id, or null for an anonymous QR table session.</summary>
    Guid? UserId { get; }

    /// <summary>The caller's role, or null when unauthenticated.</summary>
    UserRole? Role { get; }

    /// <summary>
    /// The table a QR session is locked to. Present only on tokens minted by scanning a table's QR
    /// code, and used to stop such a session ordering for any other table.
    /// </summary>
    Guid? TableId { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(UserRole role);

    /// <summary>True when the caller holds any of the given roles.</summary>
    bool IsInAnyRole(params UserRole[] roles);
}
