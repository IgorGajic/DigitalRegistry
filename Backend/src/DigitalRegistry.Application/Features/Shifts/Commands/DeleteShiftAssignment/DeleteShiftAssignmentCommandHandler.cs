using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Commands.DeleteShiftAssignment;

public class DeleteShiftAssignmentCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<DeleteShiftAssignmentCommand, Result>
{
    public async Task<Result> Handle(DeleteShiftAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await context.ShiftAssignments
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (assignment is null)
        {
            return Result.NotFound("No such assignment.");
        }

        // Shifts already generated from it survive, orphaned. The foreign key is SetNull so they keep
        // their times and simply stop claiming to belong to a standing arrangement.
        context.ShiftAssignments.Remove(assignment);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
