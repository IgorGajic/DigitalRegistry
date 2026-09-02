using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.SaveRoomLayout;

/// <summary>
/// Stores the arrangement of one room in a single write.
/// </summary>
/// <remarks>
/// The editor sends the whole room, not individual moves. Dragging a table produces a stream of
/// positions, and persisting each one would put hundreds of writes behind one gesture and leave the
/// stored layout in a half-moved state whenever the network dropped mid-drag. One request per save
/// also means the arrangement is either stored as the owner arranged it, or not at all.
/// <para>
/// Tables listed here are moved into the room; any table currently in the room but absent from the
/// list is taken out of it. That is what lets the editor remove a table from a room by dragging it
/// away, without a second endpoint for the removal.
/// </para>
/// <para>
/// Fixtures follow the same one-request rule but a different absence rule: one left out of the list
/// is <em>deleted</em>, not unassigned. A table outlives its room because it carries order history;
/// a landmark drawn nowhere is nothing at all, and there is no screen that could list it.
/// </para>
/// </remarks>
public record SaveRoomLayoutCommand(
    Guid RoomId,
    IReadOnlyList<TableLayoutRequest> Tables,
    IReadOnlyList<FixtureLayoutRequest> Fixtures) : IRequest<Result<RoomDto>>;
