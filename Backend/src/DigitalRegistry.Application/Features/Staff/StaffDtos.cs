using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Staff;

/// <summary>
/// Somebody who works at the venue.
/// </summary>
/// <param name="UserName">
/// The composite Identity user name. Shown because it is not simply the email — the restaurant code
/// is part of it, and an owner telling somebody how to sign in needs to know that.
/// </param>
/// <param name="IsEnabled">
/// False for an account switched off. Such a person keeps their name on every order and shift they
/// worked; only their ability to sign in is withdrawn.
/// </param>
public record StaffMemberDto(
    Guid Id,
    string FullName,
    string Email,
    string UserName,
    UserRole Role,
    bool IsEnabled,
    DateTime Created);
