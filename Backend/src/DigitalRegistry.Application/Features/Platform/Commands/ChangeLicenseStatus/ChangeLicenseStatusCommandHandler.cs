using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform.Commands.ChangeLicenseStatus;

public class ChangeLicenseStatusCommandHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTime)
    : IRequestHandler<ChangeLicenseStatusCommand, Result<LicenseDto>>
{
    public async Task<Result<LicenseDto>> Handle(
        ChangeLicenseStatusCommand request,
        CancellationToken cancellationToken)
    {
        var license = await context.AllLicenses()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.LicenseId, cancellationToken);

        if (license is null)
        {
            return Result<LicenseDto>.NotFound("No such licence.");
        }

        try
        {
            switch (request.Action)
            {
                case LicenseAction.Suspend:
                    license.Suspend(request.Reason);
                    break;
                case LicenseAction.Reactivate:
                    license.Reactivate();
                    break;
                case LicenseAction.Cancel:
                    license.Cancel(request.Reason);
                    break;
                default:
                    return Result<LicenseDto>.Invalid("Unknown licence action.");
            }
        }
        catch (DomainException exception)
        {
            // The domain refuses transitions that make no sense — reactivating something that is not
            // suspended, suspending a cancelled licence. That is a conflict with current state, not a
            // server fault, so it is reported rather than allowed to reach the exception middleware.
            return Result<LicenseDto>.Conflict(exception.Message);
        }

        await context.SaveChangesAsync(cancellationToken);

        var dtos = await context.AllLicenses()
            .Where(candidate => candidate.Id == license.Id)
            .ToDtosAsync(context, dateTime.UtcNow, cancellationToken);

        return Result<LicenseDto>.Success(dtos[0]);
    }
}
