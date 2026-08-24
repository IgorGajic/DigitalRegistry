using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Staff.Queries.GetStaff;

/// <summary>
/// Everybody who works at the venue.
/// </summary>
/// <remarks>
/// Guests are excluded. They are customers with accounts, not staff, and a busy venue would have far
/// more of them than of anybody the owner is actually managing.
/// </remarks>
/// <param name="IncludeDisabled">Include accounts that have been switched off.</param>
public record GetStaffQuery(bool IncludeDisabled = false) : IRequest<Result<IReadOnlyList<StaffMemberDto>>>;
