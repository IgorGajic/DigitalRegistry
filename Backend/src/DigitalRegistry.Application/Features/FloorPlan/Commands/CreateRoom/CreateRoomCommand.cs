using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.CreateRoom;

/// <summary>
/// Adds a room to the floor plan — a terrace, an upstairs, a garden.
/// </summary>
/// <param name="CanvasWidth">
/// The coordinate space tables are positioned in. Left null it takes the default, which suits a
/// typical room; a long narrow terrace is better given its own proportions.
/// </param>
public record CreateRoomCommand(
    string Name,
    int? DisplayOrder,
    int? CanvasWidth,
    int? CanvasHeight) : IRequest<Result<RoomDto>>;
