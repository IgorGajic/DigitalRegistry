using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Commands.IssueLicense;

/// <summary>
/// Issues a venue's first licence term.
/// </summary>
/// <remarks>
/// For a venue that has never been licensed. A venue that already has one is extended through
/// <c>RenewLicenseCommand</c> instead, so its term accumulates on one row rather than fragmenting into
/// several the till would have to reconcile.
/// </remarks>
public record IssueLicenseCommand(
    Guid RestaurantId,
    LicensePlan Plan,
    decimal Price,
    string? Notes) : IRequest<Result<LicenseDto>>;
