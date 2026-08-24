using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Queries.GetShiftTemplates;

public class GetShiftTemplatesQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetShiftTemplatesQuery, Result<IReadOnlyList<ShiftTemplateDto>>>
{
    public async Task<Result<IReadOnlyList<ShiftTemplateDto>>> Handle(
        GetShiftTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await context.ShiftTemplates
            .AsNoTracking()
            .Where(template => request.IncludeRetired || template.IsActive)
            .OrderBy(template => template.StartTime)
            .Select(template => new
            {
                Template = template,
                AssignmentCount = template.Assignments.Count
            })
            .ToListAsync(cancellationToken);

        var templates = rows
            .Select(row => row.Template.ToDto(row.AssignmentCount))
            .ToList();

        return Result<IReadOnlyList<ShiftTemplateDto>>.Success(templates);
    }
}
