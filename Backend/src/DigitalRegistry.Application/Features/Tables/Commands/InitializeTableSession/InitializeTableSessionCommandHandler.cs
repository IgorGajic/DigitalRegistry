using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Tables.Commands.InitializeTableSession;

public class InitializeTableSessionCommandHandler(
    IDigitalRegistryDbContext context,
    IIdentityService identityService)
    : IRequestHandler<InitializeTableSessionCommand, Result<AuthenticationResult>>
{
    /// <summary>
    /// Returned for an unknown token and for a deactivated table alike, so the endpoint cannot be
    /// used to probe which tokens exist.
    /// </summary>
    private const string InvalidTokenMessage = "That QR code is not valid.";

    public async Task<Result<AuthenticationResult>> Handle(
        InitializeTableSessionCommand request,
        CancellationToken cancellationToken)
    {
        var table = await context.Tables
            .AsNoTracking()
            .Where(candidate => candidate.QrCodeToken == request.QrCodeToken && candidate.IsActive)
            .Select(candidate => new { candidate.Id, candidate.TableNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (table is null)
        {
            return Result<AuthenticationResult>.NotFound(InvalidTokenMessage);
        }

        return identityService.IssueTableSessionToken(table.Id, table.TableNumber);
    }
}
