using System.Security.Claims;
using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Security;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Api.Services;

/// <summary>
/// Reads the caller's identity from the validated bearer token on the current request.
/// </summary>
/// <remarks>
/// Lives in the API project because it is the only layer that knows about <c>HttpContext</c>. Every
/// value comes from claims the token was signed with, so a client cannot influence them by editing a
/// request body.
/// </remarks>
public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    /// <summary>
    /// The signed-in user's id. Null for anonymous QR table sessions, whose tokens carry a table
    /// claim instead of a user identifier.
    /// </summary>
    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(Principal?.FindFirstValue(DigitalRegistryClaimTypes.Role), out var role)
            ? role
            : null;

    public Guid? TableId =>
        Guid.TryParse(Principal?.FindFirstValue(DigitalRegistryClaimTypes.TableId), out var tableId)
            ? tableId
            : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsInRole(UserRole role) => Role == role;

    public bool IsInAnyRole(params UserRole[] roles) => Role is { } role && roles.Contains(role);
}
