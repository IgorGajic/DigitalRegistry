using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetVoidReport;

public class GetVoidReportQueryHandler(IDigitalRegistryDbContext context)
    : IRequestHandler<GetVoidReportQuery, Result<VoidReportDto>>
{
    public async Task<Result<VoidReportDto>> Handle(
        GetVoidReportQuery request,
        CancellationToken cancellationToken)
    {
        var records = context.VoidRecords
            .AsNoTracking()
            .Where(record => record.VoidedAtUtc >= request.FromUtc && record.VoidedAtUtc < request.ToUtc);

        if (request.PerformedByUserId is { } userId)
        {
            records = records.Where(record => record.PerformedByUserId == userId);
        }

        if (request.Type is { } type)
        {
            records = records.Where(record => record.Type == type);
        }

        var rows = await records
            .OrderByDescending(record => record.VoidedAtUtc)
            .Select(record => new
            {
                record.Id,
                record.VoidedAtUtc,
                record.Type,
                record.OrderId,
                TableNumber = record.Order!.Table!.TableNumber,
                record.ItemName,
                record.Quantity,
                record.Amount,
                record.Reason,
                record.PerformedByUserId,
                PerformedBy = record.PerformedBy!.FirstName + " " + record.PerformedBy.LastName,
                ApprovedBy = record.ApprovedBy == null
                    ? null
                    : record.ApprovedBy.FirstName + " " + record.ApprovedBy.LastName
            })
            .ToListAsync(cancellationToken);

        var entries = rows
            .Select(row => new VoidReportEntryDto(
                row.Id,
                row.VoidedAtUtc,
                row.Type,
                row.OrderId,
                row.TableNumber,
                row.ItemName,
                row.Quantity,
                row.Amount,
                row.Reason,
                row.PerformedBy.Trim(),
                row.ApprovedBy?.Trim()))
            .ToList();

        // Grouped in memory: the rows are already loaded for the detail list, and a period's voids
        // number in the tens, not the thousands.
        var byStaff = rows
            .GroupBy(row => new { row.PerformedByUserId, row.PerformedBy })
            .Select(group => new VoidsByStaffDto(
                UserId: group.Key.PerformedByUserId,
                Name: group.Key.PerformedBy.Trim(),
                VoidCount: group.Count(),
                TotalAmount: group.Sum(row => row.Amount),
                ItemVoids: group.Count(row => row.Type == VoidType.Item),
                OpenOrderVoids: group.Count(row => row.Type == VoidType.OpenOrder),
                PaidOrderVoids: group.Count(row => row.Type == VoidType.PaidOrder)))
            // Whoever cancelled the most money is the line the owner opened this report to see.
            .OrderByDescending(entry => entry.TotalAmount)
            .ToList();

        return Result<VoidReportDto>.Success(new VoidReportDto(
            FromUtc: request.FromUtc,
            ToUtc: request.ToUtc,
            TotalVoids: entries.Count,
            TotalAmount: entries.Sum(entry => entry.Amount),
            ByStaff: byStaff,
            Entries: entries));
    }
}
