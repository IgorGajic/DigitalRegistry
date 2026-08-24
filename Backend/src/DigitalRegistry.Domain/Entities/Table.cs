using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A physical table on the floor, identified to guests by a QR code.
/// </summary>
public class Table : BaseEntity, IRestaurantScoped
{
    /// <summary>Size a table is drawn at until the owner resizes it.</summary>
    public const int DefaultSize = 80;

    /// <inheritdoc />
    public Guid RestaurantId { get; set; }

    public int TableNumber { get; set; }

    public int Capacity { get; set; }

    /// <summary>
    /// The value encoded in the table's printed QR code. Rotating it invalidates every printed
    /// code for this table, which is the intended way to revoke a leaked code.
    /// </summary>
    public Guid QrCodeToken { get; set; } = Guid.NewGuid();

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The room this table stands in, or null for one not yet placed on any floor plan.
    /// </summary>
    /// <remarks>
    /// Nullable so a table can be created from the management screen before the owner has drawn it
    /// anywhere. An unplaced table still takes orders — it simply does not appear on the floor.
    /// </remarks>
    public Guid? RoomId { get; set; }

    public Room? Room { get; set; }

    /// <summary>
    /// Where the table sits in its room's coordinate space, measured from the top-left of the shape.
    /// </summary>
    public int PositionX { get; set; }

    /// <inheritdoc cref="PositionX" />
    public int PositionY { get; set; }

    /// <summary>How large the table is drawn, in the same coordinate space as its position.</summary>
    public int Width { get; set; } = DefaultSize;

    /// <inheritdoc cref="Width" />
    public int Height { get; set; } = DefaultSize;

    public TableShape Shape { get; set; } = TableShape.Round;

    /// <summary>Clockwise rotation in degrees, for tables that do not sit square to the room.</summary>
    public int Rotation { get; set; }

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    /// <summary>Issues a fresh QR token, invalidating any previously printed code.</summary>
    public Guid RotateQrCodeToken()
    {
        QrCodeToken = Guid.NewGuid();
        return QrCodeToken;
    }

    /// <summary>True when the table can physically seat <paramref name="partySize"/> guests.</summary>
    public bool CanSeat(int partySize) => IsActive && partySize <= Capacity;
}
