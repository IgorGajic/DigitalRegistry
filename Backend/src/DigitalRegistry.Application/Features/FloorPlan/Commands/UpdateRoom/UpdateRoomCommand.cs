using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.UpdateRoom;

/// <summary>
/// Renames a room, reorders its tab, or resizes its drawing area.
/// </summary>
/// <remarks>
/// Shrinking the canvas is refused while a table would be left outside it. Allowing it would strand
/// tables beyond the edge of the editor, where they could not be dragged back.
/// </remarks>
public record UpdateRoomCommand(
    Guid Id,
    string Name,
    int DisplayOrder,
    int CanvasWidth,
    int CanvasHeight) : IRequest<Result<RoomDto>>;
