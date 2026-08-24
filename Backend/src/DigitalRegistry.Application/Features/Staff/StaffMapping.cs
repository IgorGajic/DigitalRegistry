using DigitalRegistry.Domain.Entities;

namespace DigitalRegistry.Application.Features.Staff;

internal static class StaffMapping
{
    /// <summary>
    /// True when the account can still sign in.
    /// </summary>
    /// <remarks>
    /// An account is switched off by giving it a lockout with no end date, so "enabled" is the absence
    /// of a lockout stretching into the future. A short lockout from mistyped passwords is not the
    /// same thing and is not reported as being switched off, but it does read that way while it lasts —
    /// which is honest, since the person cannot sign in either way.
    /// </remarks>
    public static bool IsEnabled(DateTimeOffset? lockoutEnd, DateTimeOffset now) =>
        lockoutEnd is null || lockoutEnd <= now;

    public static StaffMemberDto ToDto(this ApplicationUser user, DateTimeOffset now) => new(
        Id: user.Id,
        FullName: user.FullName,
        Email: user.Email ?? string.Empty,
        UserName: user.UserName ?? string.Empty,
        Role: user.Role,
        IsEnabled: IsEnabled(user.LockoutEnd, now),
        Created: default);
}
