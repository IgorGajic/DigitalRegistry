using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Staff.Commands.SetStaffEnabled;

/// <summary>
/// Switches a staff account off, or back on.
/// </summary>
/// <remarks>
/// How somebody leaves. The account is never deleted: their name stays on every order they took and
/// every shift they worked, and deleting it would take that history with it.
/// </remarks>
public record SetStaffEnabledCommand(Guid Id, bool IsEnabled) : IRequest<Result>;
