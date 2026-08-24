using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform.Commands.RecordLicensePayment;

public class RecordLicensePaymentCommandHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUser,
    IDateTimeService dateTime)
    : IRequestHandler<RecordLicensePaymentCommand, Result<LicensePaymentDto>>
{
    public async Task<Result<LicensePaymentDto>> Handle(
        RecordLicensePaymentCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } adminId)
        {
            return Result<LicensePaymentDto>.Unauthorized("The recording administrator could not be identified.");
        }

        var licenseExists = await context.AllLicenses()
            .AnyAsync(license => license.Id == request.LicenseId, cancellationToken);

        if (!licenseExists)
        {
            return Result<LicensePaymentDto>.NotFound("No such licence.");
        }

        var payment = new LicensePayment
        {
            LicenseId = request.LicenseId,
            Amount = request.Amount,
            // Payments are often entered days after they arrive, so the administrator may supply the
            // real date; today is only the fallback.
            PaidAtUtc = request.PaidAtUtc ?? dateTime.UtcNow,
            PaymentMethod = request.PaymentMethod,
            ReferenceNumber = request.ReferenceNumber?.Trim(),
            Notes = request.Notes?.Trim(),
            RecordedByAdminId = adminId
        };

        context.LicensePayments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);

        return Result<LicensePaymentDto>.Success(new LicensePaymentDto(
            Id: payment.Id,
            LicenseId: payment.LicenseId,
            Amount: payment.Amount,
            PaidAtUtc: payment.PaidAtUtc,
            PaymentMethod: payment.PaymentMethod,
            ReferenceNumber: payment.ReferenceNumber,
            Notes: payment.Notes));
    }
}
