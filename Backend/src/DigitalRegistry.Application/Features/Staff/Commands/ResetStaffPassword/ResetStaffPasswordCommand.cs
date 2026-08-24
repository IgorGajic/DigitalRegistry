using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Staff.Commands.ResetStaffPassword;

/// <summary>
/// Sets a new password for a member of staff who has forgotten theirs.
/// </summary>
/// <remarks>
/// Takes no current password, because the point is that nobody has it. There is no self-service
/// reset: a till has no email delivery behind it, and the owner is standing in the same room.
/// </remarks>
public record ResetStaffPasswordCommand(Guid Id, string NewPassword) : IRequest<Result>;
