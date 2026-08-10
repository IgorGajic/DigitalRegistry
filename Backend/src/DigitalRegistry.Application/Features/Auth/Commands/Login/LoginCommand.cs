using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Auth.Commands.Login;

/// <summary>
/// Exchanges email and password for an access token.
/// </summary>
public record LoginCommand(string Email, string Password) : IRequest<Result<AuthenticationResult>>;
