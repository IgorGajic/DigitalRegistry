using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Commands.UpdateShift;

internal sealed class UpdateShiftCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<UpdateShiftCommand, Result>
{
    public async Task<Result> Handle(UpdateShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await context.Shifts
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (shift is null)
        {
            return Result.NotFound($"Shift {request.Id} was not found.");
        }

        shift.StartTime = request.StartTime;
        shift.EndTime = request.EndTime;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
