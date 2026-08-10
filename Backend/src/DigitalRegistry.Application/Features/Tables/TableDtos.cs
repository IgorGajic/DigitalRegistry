using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Tables;

/// <summary>
/// A table as seen by a manager or owner.
/// </summary>
/// <remarks>
/// Includes <paramref name="QrCodeToken"/>, which is a credential: anyone holding it can open a
/// guest ordering session for the table. It is therefore only ever returned from the
/// management endpoints, never from the guest-facing availability query.
/// </remarks>
public record TableDto(
    Guid Id,
    int TableNumber,
    int Capacity,
    Guid QrCodeToken,
    bool IsActive);

/// <summary>
/// A table as seen by a guest looking for somewhere to sit.
/// </summary>
/// <remarks>Deliberately omits the QR token.</remarks>
public record TableAvailabilityDto(
    Guid Id,
    int TableNumber,
    int Capacity,
    TableStatus Status);

/// <summary>The result of rotating a table's QR token.</summary>
public record TableQrCodeDto(Guid TableId, int TableNumber, Guid QrCodeToken);
