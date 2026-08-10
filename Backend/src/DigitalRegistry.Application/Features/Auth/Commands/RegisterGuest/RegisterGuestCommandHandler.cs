using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Auth.Commands.RegisterGuest;

public class RegisterGuestCommandHandler(IIdentityService identityService)
    : IRequestHandler<RegisterGuestCommand, Result<AuthenticationResult>>
{
    public Task<Result<AuthenticationResult>> Handle(
        RegisterGuestCommand request,
        CancellationToken cancellationToken) =>
        identityService.RegisterGuestAsync(
            request.Email,
            request.Password,
            request.FirstName,
            request.LastName,
            cancellationToken);
}
