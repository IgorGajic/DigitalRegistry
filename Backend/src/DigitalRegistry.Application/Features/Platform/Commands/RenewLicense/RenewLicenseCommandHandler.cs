using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform.Commands.RenewLicense;

public class RenewLicenseCommandHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUser,
    IDateTimeService dateTime)
    : IRequestHandler<RenewLicenseCommand, Result<LicenseDto>>
{
    public async Task<Result<LicenseDto>> Handle(
        RenewLicenseCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return Result<LicenseDto>.Unauthorized("The renewing administrator could not be identified.");
        }

        var license = await context.AllLicenses()
            .FirstOrDefaultAsync(candidate => candidate.Id == request.LicenseId, cancellationToken);

        if (license is null)
        {
            return Result<LicenseDto>.NotFound("No such licence.");
        }

        // Renewing a cancelled licence throws in the domain; surface that as a conflict rather than
        // letting it escape as a 409 from the exception middleware with a less useful message.
        if (license.Status == Domain.Enums.LicenseStatus.Cancelled)
        {
            return Result<LicenseDto>.Conflict("A cancelled licence cannot be renewed; issue a new one.");
        }

        license.Renew(request.Plan, adminId, dateTime.UtcNow);
        license.Price = request.Price;

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            license.Notes = request.Notes;
        }

        await context.SaveChangesAsync(cancellationToken);

        var dtos = await context.AllLicenses()
            .Where(candidate => candidate.Id == license.Id)
            .ToDtosAsync(context, dateTime.UtcNow, cancellationToken);

        return Result<LicenseDto>.Success(dtos[0]);
    }
}
