using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Auth.Commands.RegisterGuest;

/// <summary>
/// Self-registration for a guest account.
/// </summary>
/// <remarks>
/// The role is not part of the command: it is always <see cref="Domain.Enums.UserRole.Guest"/>, so
/// nobody can register themselves as staff. Waiter, manager and owner accounts are provisioned
/// separately.
/// </remarks>
public record RegisterGuestCommand(
    string RestaurantSlug,
    string Email,
    string Password,
    string FirstName,
    string LastName) : IRequest<Result<AuthenticationResult>>;
