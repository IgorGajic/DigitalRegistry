using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Commands.PlatformLogin;

/// <summary>
/// Signs a platform administrator in to the master application.
/// </summary>
/// <remarks>
/// No restaurant code, because these accounts belong to no restaurant. The token issued carries no
/// restaurant claim, which is what keeps it useless against a till.
/// </remarks>
public record PlatformLoginCommand(string Email, string Password) : IRequest<Result<AuthenticationResult>>;
