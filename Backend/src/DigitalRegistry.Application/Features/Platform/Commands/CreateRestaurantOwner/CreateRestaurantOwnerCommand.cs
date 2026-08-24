using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Commands.CreateRestaurantOwner;

/// <summary>
/// Creates the owner account for a venue.
/// </summary>
/// <remarks>
/// The one staff account the platform administrator provisions. Everyone else at the venue — managers
/// and waiters — is created by the owner from inside the till, which keeps the platform out of each
/// restaurant's staffing.
/// </remarks>
public record CreateRestaurantOwnerCommand(
    Guid RestaurantId,
    string Email,
    string Password,
    string FirstName,
    string LastName) : IRequest<Result<CreatedUserDto>>;
