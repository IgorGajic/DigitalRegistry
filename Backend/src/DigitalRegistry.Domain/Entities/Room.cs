using DigitalRegistry.Domain.Common;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A part of the venue the tables are laid out in — the main room, the terrace, the upstairs floor.
/// </summary>
/// <remarks>
/// Each room is its own drawing surface. <see cref="CanvasWidth"/> and <see cref="CanvasHeight"/> give
/// the coordinate space table positions are expressed in; the client scales that space to whatever
/// screen it has, so a layout arranged on a desktop still reads correctly on a waiter's tablet.
/// </remarks>
public class Room : BaseEntity, IRestaurantScoped
{
    /// <summary>The coordinate space a room is given unless the owner resizes it.</summary>
    public const int DefaultCanvasWidth = 1200;

    /// <inheritdoc cref="DefaultCanvasWidth" />
    public const int DefaultCanvasHeight = 800;

    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Position of this room's tab on the floor screen. Lower comes first.</summary>
    public int DisplayOrder { get; set; }

    public int CanvasWidth { get; set; } = DefaultCanvasWidth;

    public int CanvasHeight { get; set; } = DefaultCanvasHeight;

    public ICollection<Table> Tables { get; set; } = new List<Table>();
}
