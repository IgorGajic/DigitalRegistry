using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Commands.SaveShiftAssignment;

public class SaveShiftAssignmentCommandHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<SaveShiftAssignmentCommand, Result<ShiftAssignmentDto>>
{
    public async Task<Result<ShiftAssignmentDto>> Handle(
        SaveShiftAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } managerId)
        {
            return Result<ShiftAssignmentDto>.Forbidden("Only a signed-in manager or owner can set the rota.");
        }

        if (!await ShiftOverlapRules.IsWaiterAsync(context, request.WaiterId, cancellationToken))
        {
            return Result<ShiftAssignmentDto>.NotFound("That account is not a waiter at this restaurant.");
        }

        var template = await context.ShiftTemplates
            .FirstOrDefaultAsync(candidate => candidate.Id == request.ShiftTemplateId, cancellationToken);

        if (template is null)
        {
            return Result<ShiftAssignmentDto>.NotFound("No such shift template.");
        }

        if (!template.IsActive)
        {
            return Result<ShiftAssignmentDto>.Conflict(
                $"'{template.Name}' has been retired and cannot take new assignments.");
        }

        ShiftAssignment assignment;

        if (request.Id is { } id)
        {
            var existing = await context.ShiftAssignments
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

            if (existing is null)
            {
                return Result<ShiftAssignmentDto>.NotFound("No such assignment.");
            }

            assignment = existing;
        }
        else
        {
            assignment = new ShiftAssignment();
            context.ShiftAssignments.Add(assignment);
        }

        assignment.WaiterId = request.WaiterId;
        assignment.ShiftTemplateId = request.ShiftTemplateId;
        assignment.Days = request.Days;
        assignment.ValidFrom = request.ValidFrom;
        assignment.ValidTo = request.ValidTo;
        assignment.AssignedByManagerId = managerId;

        // Catching a double-booking here rather than letting it surface weeks later as a generation
        // failure. Two arrangements can only clash if they share a day and overlap in date; whether
        // their hours also collide is settled against the templates below.
        var clash = await FindClashingAssignmentAsync(assignment, template, cancellationToken);

        if (clash is not null)
        {
            return Result<ShiftAssignmentDto>.Conflict(clash);
        }

        await context.SaveChangesAsync(cancellationToken);

        var waiterName = await context.Users
            .Where(user => user.Id == assignment.WaiterId)
            .Select(user => user.FirstName + " " + user.LastName)
            .FirstAsync(cancellationToken);

        return Result<ShiftAssignmentDto>.Success(new ShiftAssignmentDto(
            Id: assignment.Id,
            WaiterId: assignment.WaiterId,
            WaiterName: waiterName.Trim(),
            ShiftTemplateId: template.Id,
            ShiftTemplateName: template.Name,
            StartTime: template.StartTime,
            EndTime: template.EndTime,
            Days: assignment.Days,
            ValidFrom: assignment.ValidFrom,
            ValidTo: assignment.ValidTo));
    }

    private async Task<string?> FindClashingAssignmentAsync(
        ShiftAssignment assignment,
        ShiftTemplate template,
        CancellationToken cancellationToken)
    {
        var others = await context.ShiftAssignments
            .Where(other => other.WaiterId == assignment.WaiterId && other.Id != assignment.Id)
            .Include(other => other.ShiftTemplate)
            .ToListAsync(cancellationToken);

        foreach (var other in others.Where(other => other.SharesAnyDayWith(assignment)))
        {
            if (other.ShiftTemplate is not { } otherTemplate)
            {
                continue;
            }

            // Two shifts on the same day collide unless one finishes before the other begins. Compared
            // on the local clock, which is what the templates are stated in; a shift running past
            // midnight lands on the next day and so cannot collide with the same day's other shifts.
            if (otherTemplate.Id == template.Id)
            {
                return $"This waiter is already assigned to '{template.Name}' on one of those days.";
            }

            if (!template.CrossesMidnight && !otherTemplate.CrossesMidnight
                && template.StartTime < otherTemplate.EndTime
                && otherTemplate.StartTime < template.EndTime)
            {
                return $"'{template.Name}' overlaps '{otherTemplate.Name}', which this waiter already "
                       + "works on one of those days.";
            }
        }

        return null;
    }
}
