using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Commands.DeleteShift;

internal sealed class DeleteShiftCommandHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<DeleteShiftCommand, Result>
{
    public async Task<Result> Handle(DeleteShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await context.Shifts
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (shift is null)
        {
            return Result.NotFound($"Shift {request.Id} was not found.");
        }

        // A shift is only a roster entry; nothing references it, so it can simply go.
        context.Shifts.Remove(shift);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
