using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Licensing.Queries.GetLicenseStatus;

/// <summary>
/// Reports the signed-in restaurant's licence position.
/// </summary>
/// <remarks>
/// Takes no parameters: the restaurant comes from the caller's token, so one venue cannot ask after
/// another's licence. Reachable even while the licence is lapsed — it is what tells the client why.
/// </remarks>
public record GetLicenseStatusQuery : IRequest<Result<LicenseStatusDto>>;
