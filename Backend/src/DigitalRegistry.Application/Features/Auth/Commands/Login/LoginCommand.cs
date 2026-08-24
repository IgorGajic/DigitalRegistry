using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Auth.Commands.Login;

/// <summary>
/// Exchanges a restaurant code, email and password for an access token.
/// </summary>
/// <param name="RestaurantSlug">
/// The venue's sign-in code. An email address identifies an account only within one restaurant, so
/// this is what selects the tenant the resulting token is confined to.
/// </param>
public record LoginCommand(string RestaurantSlug, string Email, string Password)
    : IRequest<Result<AuthenticationResult>>;
