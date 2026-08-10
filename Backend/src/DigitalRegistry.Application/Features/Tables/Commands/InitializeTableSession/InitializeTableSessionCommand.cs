using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Tables.Commands.InitializeTableSession;

/// <summary>
/// Trades a table's QR token for a short-lived, table-scoped access token.
/// </summary>
/// <remarks>
/// Open to anonymous callers: a guest scans the code before they have any account. The returned
/// token permits viewing the menu and ordering for that one table and nothing else.
/// </remarks>
public record InitializeTableSessionCommand(Guid QrCodeToken) : IRequest<Result<AuthenticationResult>>;
