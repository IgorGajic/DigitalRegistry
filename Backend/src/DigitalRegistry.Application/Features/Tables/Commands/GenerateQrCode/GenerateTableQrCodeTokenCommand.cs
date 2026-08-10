using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Tables.Commands.GenerateQrCode;

/// <summary>
/// Issues a fresh QR token for a table, invalidating every previously printed code for it.
/// Manager and owner only.
/// </summary>
/// <remarks>
/// This is the revocation mechanism: if a table's printed code is photographed and shared, rotating
/// the token stops the old one opening new sessions. Tokens already exchanged for a session JWT keep
/// working until that JWT expires, which is why table sessions are short-lived.
/// </remarks>
public record GenerateTableQrCodeTokenCommand(Guid TableId) : IRequest<Result<TableQrCodeDto>>;
