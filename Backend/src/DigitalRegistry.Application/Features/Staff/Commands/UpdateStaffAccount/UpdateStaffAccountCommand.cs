using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Staff.Commands.UpdateStaffAccount;

/// <summary>
/// Corrects a staff member's name, or moves them between waiter and manager.
/// </summary>
/// <remarks>
/// The email is not changeable. It forms half of the Identity user name, so altering it would change
/// how the person signs in — a rename that silently locks somebody out. Somebody who needs a
/// different address gets a new account.
/// </remarks>
public record UpdateStaffAccountCommand(
    Guid Id,
    string FirstName,
    string LastName,
    UserRole Role) : IRequest<Result<StaffMemberDto>>;
