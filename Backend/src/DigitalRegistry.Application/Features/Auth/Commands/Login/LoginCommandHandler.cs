using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler(IIdentityService identityService)
    : IRequestHandler<LoginCommand, Result<AuthenticationResult>>
{
    public Task<Result<AuthenticationResult>> Handle(LoginCommand request, CancellationToken cancellationToken) =>
        identityService.LoginAsync(request.Email, request.Password, cancellationToken);
}
