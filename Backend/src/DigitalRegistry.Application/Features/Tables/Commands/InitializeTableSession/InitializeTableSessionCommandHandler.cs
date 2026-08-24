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
        // The one query in the system that must see across restaurants. A guest scanning a QR code
        // has no token yet, so there is no tenant to filter by — the token itself is what identifies
        // the restaurant, which is why it is unique platform-wide. Everything the resulting session
        // goes on to do is confined to the restaurant resolved here.
        var table = await context.Tables
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(candidate => candidate.QrCodeToken == request.QrCodeToken && candidate.IsActive)
            .Select(candidate => new { candidate.Id, candidate.RestaurantId, candidate.TableNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (table is null)
        {
            return Result<AuthenticationResult>.NotFound(InvalidTokenMessage);
        }

        return identityService.IssueTableSessionToken(table.RestaurantId, table.Id, table.TableNumber);
    }
}
