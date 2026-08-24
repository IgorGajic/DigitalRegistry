using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Queries.GetShiftAssignments;

public class GetShiftAssignmentsQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetShiftAssignmentsQuery, Result<IReadOnlyList<ShiftAssignmentDto>>>
{
    public async Task<Result<IReadOnlyList<ShiftAssignmentDto>>> Handle(
        GetShiftAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        var assignments = context.ShiftAssignments.AsNoTracking();

        if (request.WaiterId is { } waiterId)
        {
            assignments = assignments.Where(assignment => assignment.WaiterId == waiterId);
        }

        if (request.OnDate is { } date)
        {
            assignments = assignments.Where(assignment =>
                assignment.ValidFrom <= date && (assignment.ValidTo == null || assignment.ValidTo >= date));
        }

        var results = await assignments
            .OrderBy(assignment => assignment.Waiter!.FirstName)
            .ThenBy(assignment => assignment.ShiftTemplate!.StartTime)
            .Select(assignment => new ShiftAssignmentDto(
                assignment.Id,
                assignment.WaiterId,
                assignment.Waiter!.FirstName + " " + assignment.Waiter.LastName,
                assignment.ShiftTemplateId,
                assignment.ShiftTemplate!.Name,
                assignment.ShiftTemplate.StartTime,
                assignment.ShiftTemplate.EndTime,
                assignment.Days,
                assignment.ValidFrom,
                assignment.ValidTo))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ShiftAssignmentDto>>.Success(results);
    }
}
