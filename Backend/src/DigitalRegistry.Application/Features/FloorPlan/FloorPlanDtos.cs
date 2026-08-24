using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.FloorPlan;

/// <summary>A room and everything drawn in it.</summary>
/// <param name="CanvasWidth">
/// The coordinate space table positions are expressed in. The client scales this to its own viewport,
/// so a layout arranged on a desktop still reads correctly on a tablet.
/// </param>
public record RoomDto(
    Guid Id,
    string Name,
    int DisplayOrder,
    int CanvasWidth,
    int CanvasHeight,
    IReadOnlyList<FloorPlanTableDto> Tables);

/// <summary>
/// A table as drawn on the floor screen: where it sits, and what is happening at it.
/// </summary>
/// <remarks>
/// Carries no QR token. This is the screen every waiter has open all shift, and the token is a
/// credential that belongs only on the management endpoints.
/// </remarks>
/// <param name="OpenOrderIds">
/// The tabs currently running, oldest first. Usually none or one, but a table may carry several at
/// once — separate parties, or rounds kept apart — so the client offers a choice rather than
/// assuming. Ids rather than a count because this is the only place the till learns which tab a
/// table is running: without them a waiter returning to an occupied table would face an empty bill.
/// </param>
/// <param name="OpenOrderTotal">Sum of those tabs, for the amount shown on the table.</param>
/// <param name="OldestOpenOrderAtUtc">
/// When the earliest tab was opened, so the floor screen can show how long a table has been sitting.
/// </param>
public record FloorPlanTableDto(
    Guid Id,
    int TableNumber,
    int Capacity,
    TableStatus Status,
    TableShape Shape,
    int PositionX,
    int PositionY,
    int Width,
    int Height,
    int Rotation,
    bool IsActive,
    IReadOnlyList<Guid> OpenOrderIds,
    decimal OpenOrderTotal,
    DateTime? OldestOpenOrderAtUtc);

/// <summary>The whole floor: every room, in tab order, plus anything not yet placed.</summary>
/// <param name="UnplacedTables">
/// Tables belonging to no room. They still take orders; they simply have not been drawn anywhere yet,
/// and the layout editor lists them so the owner can place them.
/// </param>
public record FloorPlanDto(
    IReadOnlyList<RoomDto> Rooms,
    IReadOnlyList<FloorPlanTableDto> UnplacedTables);

/// <summary>One table's position, as sent by the layout editor.</summary>
public record TableLayoutRequest(
    Guid TableId,
    int PositionX,
    int PositionY,
    int Width,
    int Height,
    TableShape Shape,
    int Rotation);
