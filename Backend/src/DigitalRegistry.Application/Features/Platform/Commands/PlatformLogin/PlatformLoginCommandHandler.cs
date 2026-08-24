using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Commands.PlatformLogin;

public class PlatformLoginCommandHandler(IIdentityService identityService)
    : IRequestHandler<PlatformLoginCommand, Result<AuthenticationResult>>
{
    public Task<Result<AuthenticationResult>> Handle(
        PlatformLoginCommand request,
        CancellationToken cancellationToken) =>
        identityService.LoginPlatformAdminAsync(request.Email, request.Password, cancellationToken);
}
