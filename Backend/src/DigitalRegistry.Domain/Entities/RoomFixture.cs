using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// Something drawn on a floor plan that is not a table: the bar, the restrooms, the way in.
/// </summary>
/// <remarks>
/// A floor plan of tables alone is a grid of circles a waiter has to translate into a room. These
/// give it landmarks, so the plan reads the way the room does when you walk into it.
/// <para>
/// Deliberately its own entity rather than a <see cref="Table"/> flagged inactive. A table that is
/// not a table would still be reachable by everything that looks tables up — reservations, QR
/// tokens, seating capacity, the order screen — and each of those would have to learn to skip it.
/// Nothing here can be ordered from, booked, or sat at, because nothing here is a table.
/// </para>
/// </remarks>
public class RoomFixture : BaseEntity, IRestaurantScoped
{
    /// <summary>Longest label a fixture may carry, which is about what fits when it is drawn.</summary>
    public const int MaxLabelLength = 30;

    /// <summary>Smallest a fixture may be drawn, in room coordinates.</summary>
    public const int MinSize = 20;

    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    /// <summary>
    /// The room this fixture is drawn in.
    /// </summary>
    /// <remarks>
    /// Required, unlike <see cref="Table.RoomId"/>. An unplaced table still takes orders and simply
    /// has not been drawn yet; an unplaced landmark is not anything at all.
    /// </remarks>
    public Guid RoomId { get; set; }

    public Room? Room { get; set; }

    public FixtureKind Kind { get; set; } = FixtureKind.Other;

    /// <summary>
    /// What is written on the fixture.
    /// </summary>
    /// <remarks>
    /// Seeded from <see cref="Kind"/> by the editor but freely editable, because a venue with two
    /// restrooms needs to tell them apart and the list of kinds cannot know that.
    /// </remarks>
    public string Label { get; set; } = string.Empty;

    public FixtureShape Shape { get; set; } = FixtureShape.Rectangle;

    public FixtureTone Tone { get; set; } = FixtureTone.Stone;

    /// <summary>
    /// Where the fixture sits in its room's coordinate space, measured from the top-left corner.
    /// </summary>
    public int PositionX { get; set; }

    /// <inheritdoc cref="PositionX" />
    public int PositionY { get; set; }

    /// <summary>How large the fixture is drawn, in the same coordinate space as its position.</summary>
    public int Width { get; set; } = 160;

    /// <inheritdoc cref="Width" />
    public int Height { get; set; } = 60;

    /// <summary>Clockwise rotation in degrees, for anything not square to the room.</summary>
    public int Rotation { get; set; }

    /// <summary>
    /// Order among fixtures, lower drawn first.
    /// </summary>
    /// <remarks>
    /// Only ever orders fixtures against each other — every fixture is drawn beneath every table.
    /// A bar covering a table would hide what that table owes, which is the one number the floor
    /// screen exists to show.
    /// </remarks>
    public int DisplayOrder { get; set; }
}
