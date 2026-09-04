using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Features.Reports;
using DigitalRegistry.Application.Features.Reports.Queries.GetWaiterPerformance;
using DigitalRegistry.Application.UnitTests.TestDoubles;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Infrastructure.Persistence;
using Xunit;

namespace DigitalRegistry.Application.UnitTests.Reports;

/// <summary>
/// The owner's per-waiter report.
/// </summary>
/// <remarks>
/// The three things worth pinning here are the three the report could quietly get wrong: who a round
/// belongs to, which shifts count, and what happens to a waiter nothing can be measured about.
/// </remarks>
public class GetWaiterPerformanceQueryHandlerTests
{
    /// <summary>Belgrade is UTC+2 in September, so a local day starts at 22:00 the evening before.</summary>
    private static readonly DateOnly Day = new(2026, 9, 10);

    [Fact]
    public async Task Report_CreditsAGuestRoundToWhoeverCarriedItOut()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (marko, jelena, table) = await SeedAsync(context, tenant);

        // Nobody took this round — it came from a phone — and Jelena carried it out.
        context.Orders.Add(Round(tenant, table, waiterId: null, servedById: jelena.Id, minutesToServe: 6));
        await context.SaveChangesAsync();

        var report = await HandleAsync(context, tenant);

        var row = Assert.Single(report.Waiters);
        Assert.Equal("Jelena Jelic", row.Name);
        Assert.Equal(1, row.OrderCount);
        Assert.Equal(6, row.AverageServiceMinutes);
        Assert.Equal(1, row.TimedOrderCount);

        // Marko has neither a round nor a shift in the period, so he is not a row at all.
        Assert.DoesNotContain(report.Waiters, waiter => waiter.WaiterId == marko.Id);
    }

    [Fact]
    public async Task Report_FallsBackToWhoeverTookTheRoundWhenNobodyPressedServed()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (marko, _, table) = await SeedAsync(context, tenant);

        context.Orders.Add(Round(tenant, table, waiterId: marko.Id, servedById: null, minutesToServe: null));
        await context.SaveChangesAsync();

        var report = await HandleAsync(context, tenant);

        var row = Assert.Single(report.Waiters);
        Assert.Equal(marko.Id, row.WaiterId);
        Assert.Equal(1, row.OrderCount);
        // Nothing timed it, so there is no average — not a zero, which would read as instant service.
        Assert.Null(row.AverageServiceMinutes);
        Assert.Equal(0, row.TimedOrderCount);
    }

    [Fact]
    public async Task Report_LeavesOutCancelledAndReversedRounds()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (marko, _, table) = await SeedAsync(context, tenant);

        var cancelled = Round(tenant, table, marko.Id, null, null);
        cancelled.Status = OrderStatus.Cancelled;

        var reversed = Round(tenant, table, marko.Id, null, null);
        reversed.Status = OrderStatus.Voided;

        context.Orders.AddRange(cancelled, reversed, Round(tenant, table, marko.Id, null, null));
        await context.SaveChangesAsync();

        var report = await HandleAsync(context, tenant);

        var row = Assert.Single(report.Waiters);
        // One of the three. Otherwise the report would reward ringing things up and taking them off.
        Assert.Equal(1, row.OrderCount);
        Assert.Equal(200m, row.TotalValue);
    }

    [Fact]
    public async Task Report_CountsOnlyThePartOfAShiftInsideThePeriod()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (marko, _, _) = await SeedAsync(context, tenant);

        // 20:00 to 04:00 local on the report's single day: four of its eight hours fall after
        // midnight, and the period ends at midnight.
        context.Shifts.Add(new Shift
        {
            RestaurantId = tenant.RestaurantId,
            WaiterId = marko.Id,
            StartTime = new DateTime(2026, 9, 10, 18, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 9, 11, 2, 0, 0, DateTimeKind.Utc),
            AssignedByManagerId = Guid.NewGuid()
        });

        await context.SaveChangesAsync();

        var report = await HandleAsync(context, tenant);

        var row = Assert.Single(report.Waiters);
        Assert.Equal(4, row.HoursWorked);
        // No rounds at all, so there is no rate to state rather than a division by nothing.
        Assert.Equal(0, row.OrderCount);
        Assert.Equal(0m, row.ValuePerHour);
    }

    private static async Task<WaiterPerformanceReportDto> HandleAsync(
        ApplicationDbContext context,
        TestTenantContext tenant)
    {
        var handler = new GetWaiterPerformanceQueryHandler(context, tenant);
        var result = await handler.Handle(new GetWaiterPerformanceQuery(Day, Day), CancellationToken.None);

        Assert.True(result.Succeeded);

        return result.Value!;
    }

    /// <summary>One 200 RSD round placed at 20:00 local on the report's day.</summary>
    private static Order Round(
        TestTenantContext tenant,
        Table table,
        Guid? waiterId,
        Guid? servedById,
        double? minutesToServe)
    {
        var placedAt = new DateTime(2026, 9, 10, 18, 0, 0, DateTimeKind.Utc);

        var order = new Order
        {
            RestaurantId = tenant.RestaurantId,
            TableId = table.Id,
            WaiterId = waiterId,
            ServedByWaiterId = servedById,
            Status = servedById is null ? OrderStatus.Open : OrderStatus.Served,
            CreatedAt = placedAt,
            ServedAtUtc = minutesToServe is { } minutes ? placedAt.AddMinutes(minutes) : null
        };

        order.OrderItems.Add(new OrderItem
        {
            RestaurantId = tenant.RestaurantId,
            OrderId = order.Id,
            MenuItemId = Guid.NewGuid(),
            Quantity = 2,
            UnitPrice = 100m
        });

        return order;
    }

    private static async Task<(ApplicationUser Marko, ApplicationUser Jelena, Table Table)> SeedAsync(
        ApplicationDbContext context,
        TestTenantContext tenant)
    {
        context.Restaurants.Add(new Restaurant
        {
            Id = tenant.RestaurantId,
            Name = "Demo",
            Slug = "demo",
            TimeZoneId = "Europe/Belgrade"
        });

        var marko = Staff(tenant, "Marko", "Markovic");
        var jelena = Staff(tenant, "Jelena", "Jelic");

        var table = new Table
        {
            RestaurantId = tenant.RestaurantId,
            TableNumber = 1,
            Capacity = 4,
            QrCodeToken = Guid.NewGuid()
        };

        context.Users.AddRange(marko, jelena);
        context.Tables.Add(table);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return (marko, jelena, table);
    }

    private static ApplicationUser Staff(TestTenantContext tenant, string first, string last) => new()
    {
        UserName = $"demo|{first.ToLowerInvariant()}@example.com",
        Email = $"{first.ToLowerInvariant()}@example.com",
        FirstName = first,
        LastName = last,
        Role = UserRole.Waiter,
        RestaurantId = tenant.RestaurantId
    };
}
