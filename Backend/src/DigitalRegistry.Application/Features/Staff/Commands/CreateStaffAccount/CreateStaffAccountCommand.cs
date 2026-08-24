using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Staff.Commands.CreateStaffAccount;

/// <summary>
/// Takes somebody on: a waiter or a manager.
/// </summary>
/// <remarks>
/// The owner's own account comes from the platform when the venue is registered; everybody else is
/// created here. Without this a restaurant would have exactly one account, and the rota would be
/// assigning waiters who cannot exist.
/// </remarks>
/// <param name="Role">
/// Only <see cref="UserRole.Waiter"/> and <see cref="UserRole.Manager"/>. A second owner is refused
/// here, and a guest account is something people create for themselves.
/// </param>
public record CreateStaffAccountCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    UserRole Role) : IRequest<Result<StaffMemberDto>>;
