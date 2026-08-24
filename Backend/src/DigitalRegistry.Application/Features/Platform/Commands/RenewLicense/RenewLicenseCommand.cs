using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Commands.RenewLicense;

/// <summary>
/// Extends a venue's licence by another term.
/// </summary>
/// <remarks>
/// Also the way a suspended venue is let back in, since paying is normally what resolves a suspension.
/// A venue that renews before lapsing is extended from its existing end date rather than from today,
/// so paying early costs it nothing.
/// </remarks>
public record RenewLicenseCommand(
    Guid LicenseId,
    LicensePlan Plan,
    decimal Price,
    string? Notes) : IRequest<Result<LicenseDto>>;
