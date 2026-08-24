using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Staff.Queries.GetStaff;

public class GetStaffQueryHandler(
    IDigitalRegistryDbContext context,
    ITenantContext tenant,
    IDateTimeService dateTime)
    : IRequestHandler<GetStaffQuery, Result<IReadOnlyList<StaffMemberDto>>>
{
    public async Task<Result<IReadOnlyList<StaffMemberDto>>> Handle(
        GetStaffQuery request,
        CancellationToken cancellationToken)
    {
        // Users are not restaurant-scoped — Identity owns that table and queries it through its own
        // stores — so this narrows by the token's restaurant explicitly.
        var rows = await context.Users
            .AsNoTracking()
            .Where(user => user.RestaurantId == tenant.RestaurantId && user.Role != UserRole.Guest)
            .OrderBy(user => user.Role)
            .ThenBy(user => user.FirstName)
            .Select(user => new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.UserName,
                user.Role,
                user.LockoutEnd
            })
            .ToListAsync(cancellationToken);

        var now = new DateTimeOffset(dateTime.UtcNow, TimeSpan.Zero);

        var staff = rows
            .Select(row => new StaffMemberDto(
                Id: row.Id,
                FullName: $"{row.FirstName} {row.LastName}".Trim(),
                Email: row.Email ?? string.Empty,
                UserName: row.UserName ?? string.Empty,
                Role: row.Role,
                IsEnabled: StaffMapping.IsEnabled(row.LockoutEnd, now),
                Created: default))
            .Where(member => request.IncludeDisabled || member.IsEnabled)
            .ToList();

        return Result<IReadOnlyList<StaffMemberDto>>.Success(staff);
    }
}
