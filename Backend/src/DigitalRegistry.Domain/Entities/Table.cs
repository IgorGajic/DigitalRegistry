using DigitalRegistry.Domain.Common;

namespace DigitalRegistry.Domain.Entities;

/// <summary>
/// A physical table on the floor, identified to guests by a QR code.
/// </summary>
public class Table : BaseEntity
{
    public int TableNumber { get; set; }

    public int Capacity { get; set; }

    /// <summary>
    /// The value encoded in the table's printed QR code. Rotating it invalidates every printed
    /// code for this table, which is the intended way to revoke a leaked code.
    /// </summary>
    public Guid QrCodeToken { get; set; } = Guid.NewGuid();

    public bool IsActive { get; set; } = true;

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
