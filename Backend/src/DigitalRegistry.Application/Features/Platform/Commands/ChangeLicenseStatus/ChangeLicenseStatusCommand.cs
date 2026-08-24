using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Commands.ChangeLicenseStatus;

/// <summary>What the administrator wants done to a licence.</summary>
public enum LicenseAction
{
    /// <summary>Switch the venue off mid-term without ending the contract.</summary>
    Suspend = 1,

    /// <summary>Lift a suspension, restoring whatever time the term had left.</summary>
    Reactivate = 2,

    /// <summary>End the licence for good.</summary>
    Cancel = 3
}

/// <summary>
/// Suspends, reactivates or cancels a licence.
/// </summary>
/// <remarks>
/// One command for all three because they are the same decision — an administrator changing a venue's
/// standing — and each carries the same obligation to say why. Splitting them would triple the
/// plumbing to express that once.
/// </remarks>
public record ChangeLicenseStatusCommand(
    Guid LicenseId,
    LicenseAction Action,
    string Reason) : IRequest<Result<LicenseDto>>;
