using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Commands.SaveShiftTemplate;

public class SaveShiftTemplateCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<SaveShiftTemplateCommand, Result<ShiftTemplateDto>>
{
    public async Task<Result<ShiftTemplateDto>> Handle(
        SaveShiftTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        var nameTaken = await context.ShiftTemplates
            .AnyAsync(other => other.Name == name && other.Id != request.Id, cancellationToken);

        if (nameTaken)
        {
            return Result<ShiftTemplateDto>.Conflict($"A shift called '{name}' already exists.");
        }

        ShiftTemplate template;

        if (request.Id is { } id)
        {
            var existing = await context.ShiftTemplates
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            if (existing is null)
            {
                return Result<ShiftTemplateDto>.NotFound("No such shift template.");
            }

            template = existing;
        }
        else
        {
            template = new ShiftTemplate();
            context.ShiftTemplates.Add(template);
        }

        template.Name = name;
        template.StartTime = request.StartTime;
        template.EndTime = request.EndTime;
        template.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        var assignmentCount = await context.ShiftAssignments
            .CountAsync(assignment => assignment.ShiftTemplateId == template.Id, cancellationToken);

        return Result<ShiftTemplateDto>.Success(template.ToDto(assignmentCount));
    }
}
