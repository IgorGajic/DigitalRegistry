using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Commands.AssignShift;

internal sealed class AssignShiftCommandHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<AssignShiftCommand, Result<ShiftDto>>
{
    public async Task<Result<ShiftDto>> Handle(AssignShiftCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } managerId)
        {
            return Result<ShiftDto>.Forbidden("Only a signed-in manager or owner can assign shifts.");
        }

        // The waiter's existence, role and freedom from clashes were all settled by the validator
        // before this ran, so all that is left is to record the assignment.
        var shift = new Shift
        {
            WaiterId = request.WaiterId,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            AssignedByManagerId = managerId
        };

        context.Shifts.Add(shift);
        await context.SaveChangesAsync(cancellationToken);

        var waiterName = await context.Users
            .Where(user => user.Id == shift.WaiterId)
            .Select(user => user.FirstName + " " + user.LastName)
            .FirstAsync(cancellationToken);

        return Result<ShiftDto>.Success(new ShiftDto(
            shift.Id,
            shift.WaiterId,
            waiterName,
            shift.StartTime,
            shift.EndTime,
            shift.AssignedByManagerId));
    }
}
