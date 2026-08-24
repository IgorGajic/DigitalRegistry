using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Platform.Queries.GetPlatformDashboard;

/// <summary>
/// Headline figures for the master application's landing page.
/// </summary>
/// <param name="RevenueMonths">How many months of licence revenue to chart.</param>
public record GetPlatformDashboardQuery(int RevenueMonths = 12) : IRequest<Result<PlatformDashboardDto>>;
