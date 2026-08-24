using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform.Queries.GetLicensePayments;

public class GetLicensePaymentsQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetLicensePaymentsQuery, Result<IReadOnlyList<LicensePaymentDto>>>
{
    public async Task<Result<IReadOnlyList<LicensePaymentDto>>> Handle(
        GetLicensePaymentsQuery request,
        CancellationToken cancellationToken)
    {
        var licenseExists = await context.AllLicenses()
            .AnyAsync(license => license.Id == request.LicenseId, cancellationToken);

        if (!licenseExists)
        {
            return Result<IReadOnlyList<LicensePaymentDto>>.NotFound("No such licence.");
        }

        var payments = await context.AllLicensePayments()
            .AsNoTracking()
            .Where(payment => payment.LicenseId == request.LicenseId)
            .OrderByDescending(payment => payment.PaidAtUtc)
            .Select(payment => new LicensePaymentDto(
                payment.Id,
                payment.LicenseId,
                payment.Amount,
                payment.PaidAtUtc,
                payment.PaymentMethod,
                payment.ReferenceNumber,
                payment.Notes))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<LicensePaymentDto>>.Success(payments);
    }
}
