using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Queries.GetShiftTemplates;

/// <summary>The venue's named working periods.</summary>
/// <param name="IncludeRetired">Include templates no longer offered for new assignments.</param>
public record GetShiftTemplatesQuery(bool IncludeRetired = false)
    : IRequest<Result<IReadOnlyList<ShiftTemplateDto>>>;
