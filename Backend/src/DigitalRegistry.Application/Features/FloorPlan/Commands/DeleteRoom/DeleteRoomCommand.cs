using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.DeleteRoom;

/// <summary>
/// Removes a room from the floor plan.
/// </summary>
/// <remarks>
/// The tables in it are kept, unplaced — they carry order history, and rearranging the floor must
/// never be a way to lose it. They reappear in the editor's list of tables awaiting a place.
/// </remarks>
public record DeleteRoomCommand(Guid Id) : IRequest<Result>;
