using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Commands.RecordLicensePayment;

/// <summary>
/// Records money received against a licence.
/// </summary>
/// <remarks>
/// Bookkeeping only. It does not extend the term and does not put a lapsed venue back in service —
/// renewal does that, and keeping the two apart means an administrator can log a part payment without
/// accidentally granting a month.
/// </remarks>
public record RecordLicensePaymentCommand(
    Guid LicenseId,
    decimal Amount,
    DateTime? PaidAtUtc,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Notes) : IRequest<Result<LicensePaymentDto>>;
