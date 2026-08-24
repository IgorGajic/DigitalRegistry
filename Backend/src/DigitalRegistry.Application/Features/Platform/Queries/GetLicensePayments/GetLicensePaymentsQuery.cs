using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Queries.GetLicensePayments;

/// <summary>Lists the payments recorded against one licence, newest first.</summary>
public record GetLicensePaymentsQuery(Guid LicenseId) : IRequest<Result<IReadOnlyList<LicensePaymentDto>>>;
