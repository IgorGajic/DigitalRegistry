using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetVoidReport;

/// <summary>
/// Everything cancelled over a period, and by whom.
/// </summary>
/// <remarks>
/// The reason the void records are written at all. Voids are the easiest way for takings to leak out
/// of a till, and the defence is not blocking them — staff need them — but making the pattern visible.
/// </remarks>
/// <param name="PerformedByUserId">Narrows to one member of staff.</param>
public record GetVoidReportQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? PerformedByUserId = null,
    VoidType? Type = null) : IRequest<Result<VoidReportDto>>;
