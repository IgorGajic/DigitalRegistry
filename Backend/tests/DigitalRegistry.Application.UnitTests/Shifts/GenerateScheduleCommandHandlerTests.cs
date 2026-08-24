using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Features.Shifts;
using DigitalRegistry.Application.Features.Shifts.Commands.GenerateSchedule;
using DigitalRegistry.Application.UnitTests.TestDoubles;
using DigitalRegistry.Domain.Entities;
using DigitalRegistry.Domain.Enums;
using DigitalRegistry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace DigitalRegistry.Application.UnitTests.Shifts;

/// <summary>
/// Turning standing arrangements into actual shifts.
/// </summary>
public class GenerateScheduleCommandHandlerTests
{
    private static readonly Guid ManagerId = Guid.NewGuid();

    /// <summary>A Monday, so week-day arithmetic in the tests reads plainly.</summary>
    private static readonly DateOnly WeekStart = new(2026, 9, 7);

    [Fact]
    public async Task Generate_WritesOneShiftPerMatchingDay()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (waiter, template) = await SeedAsync(context, tenant, "15:00", "23:00");
        await AssignAsync(context, waiter, template, WeekDays.Weekdays, WeekStart, WeekStart.AddDays(13));

        // Two full weeks: ten weekdays, four weekend days skipped.
        var result = await Handle(context, tenant, new GenerateScheduleCommand(WeekStart, WeekStart.AddDays(13)));

        Assert.True(result.Succeeded);
        Assert.Equal(10, result.Value.Created);
        Assert.Empty(result.Value.Conflicts);
        Assert.Equal(10, await context.Shifts.CountAsync());

        var days = await context.Shifts.Select(shift => shift.StartTime.DayOfWeek).Distinct().ToListAsync();
        Assert.DoesNotContain(DayOfWeek.Saturday, days);
        Assert.DoesNotContain(DayOfWeek.Sunday, days);
    }

    [Fact]
    public async Task Generate_RunningTwiceDoesNotDuplicate()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (waiter, template) = await SeedAsync(context, tenant, "15:00", "23:00");
        await AssignAsync(context, waiter, template, WeekDays.Weekdays, WeekStart, WeekStart.AddDays(6));

        var first = await Handle(context, tenant, new GenerateScheduleCommand(WeekStart, WeekStart.AddDays(6)));
        var second = await Handle(context, tenant, new GenerateScheduleCommand(WeekStart, WeekStart.AddDays(6)));

        Assert.Equal(5, first.Value.Created);
        // The second run recognises its own output and tops up nothing.
        Assert.Equal(0, second.Value.Created);
        Assert.Equal(5, second.Value.AlreadyPresent);
        Assert.Equal(5, await context.Shifts.CountAsync());
    }

    [Fact]
    public async Task Generate_ExtendingTheRangeTopsUpWhatIsMissing()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (waiter, template) = await SeedAsync(context, tenant, "15:00", "23:00");
        await AssignAsync(context, waiter, template, WeekDays.Weekdays, WeekStart, WeekStart.AddDays(13));

        await Handle(context, tenant, new GenerateScheduleCommand(WeekStart, WeekStart.AddDays(6)));

        // A manager extending the rota should not have to work out where the last run stopped.
        var second = await Handle(context, tenant, new GenerateScheduleCommand(WeekStart, WeekStart.AddDays(13)));

        Assert.Equal(5, second.Value.Created);
        Assert.Equal(5, second.Value.AlreadyPresent);
        Assert.Equal(10, await context.Shifts.CountAsync());
    }

    [Fact]
    public async Task Generate_ReportsAClashRatherThanSkippingItSilently()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (waiter, template) = await SeedAsync(context, tenant, "15:00", "23:00");
        await AssignAsync(context, waiter, template, WeekDays.Monday, WeekStart, WeekStart);

        // A cover shift entered by hand that overlaps the arrangement's hours.
        var timeZone = ShiftClock.ResolveTimeZone("Europe/Belgrade");
        context.Shifts.Add(new Shift
        {
            WaiterId = waiter.Id,
            StartTime = ShiftClock.ToUtc(WeekStart.ToDateTime(new TimeOnly(18, 0)), timeZone),
            EndTime = ShiftClock.ToUtc(WeekStart.ToDateTime(new TimeOnly(22, 0)), timeZone),
            AssignedByManagerId = ManagerId
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Handle(context, tenant, new GenerateScheduleCommand(WeekStart, WeekStart));

        Assert.Equal(0, result.Value.Created);
        var conflict = Assert.Single(result.Value.Conflicts);
        Assert.Equal(WeekStart, conflict.Date);
        Assert.Equal(waiter.Id, conflict.WaiterId);
    }

    [Fact]
    public async Task Generate_UsesTheVenueClockNotTheServerClock()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (waiter, template) = await SeedAsync(context, tenant, "15:00", "23:00");
        await AssignAsync(context, waiter, template, WeekDays.Monday, WeekStart, WeekStart);

        await Handle(context, tenant, new GenerateScheduleCommand(WeekStart, WeekStart));

        var shift = await context.Shifts.SingleAsync();

        // 7 September is summer time in Belgrade, UTC+2, so 15:00 local is stored as 13:00.
        Assert.Equal(new DateTime(2026, 9, 7, 13, 0, 0, DateTimeKind.Utc), shift.StartTime);
        Assert.Equal(new DateTime(2026, 9, 7, 21, 0, 0, DateTimeKind.Utc), shift.EndTime);
    }

    [Fact]
    public async Task Generate_CarriesANightShiftIntoTheFollowingDay()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (waiter, template) = await SeedAsync(context, tenant, "22:00", "06:00");
        await AssignAsync(context, waiter, template, WeekDays.Monday, WeekStart, WeekStart);

        await Handle(context, tenant, new GenerateScheduleCommand(WeekStart, WeekStart));

        var shift = await context.Shifts.SingleAsync();

        Assert.Equal(8, (shift.EndTime - shift.StartTime).TotalHours);
        Assert.Equal(8, shift.EndTime.Day);
    }

    [Fact]
    public async Task Generate_IgnoresARetiredTemplate()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (waiter, template) = await SeedAsync(context, tenant, "15:00", "23:00");
        await AssignAsync(context, waiter, template, WeekDays.All, WeekStart, WeekStart.AddDays(6));

        // Reloaded because the seed helper detaches everything it created; editing the stale instance
        // would change nothing in the database and the test would pass for the wrong reason.
        var retired = await context.ShiftTemplates.SingleAsync(candidate => candidate.Id == template.Id);
        retired.IsActive = false;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Handle(context, tenant, new GenerateScheduleCommand(WeekStart, WeekStart.AddDays(6)));

        Assert.Equal(0, result.Value.Created);
    }

    [Fact]
    public async Task Generate_StaysInsideTheArrangementPeriod()
    {
        await using var context = TestDbContextFactory.Create(out var tenant);
        var (waiter, template) = await SeedAsync(context, tenant, "15:00", "23:00");

        // Runs for three days only, though a fortnight is generated.
        await AssignAsync(context, waiter, template, WeekDays.All, WeekStart, WeekStart.AddDays(2));

        var result = await Handle(context, tenant, new GenerateScheduleCommand(WeekStart, WeekStart.AddDays(13)));

        Assert.Equal(3, result.Value.Created);
    }

    private static Task<Result<GenerateScheduleResultDto>> Handle(
        ApplicationDbContext context,
        TestTenantContext tenant,
        GenerateScheduleCommand command)
    {
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(ManagerId);

        return new GenerateScheduleCommandHandler(context, currentUser, tenant)
            .Handle(command, CancellationToken.None);
    }

    private static async Task<(ApplicationUser Waiter, ShiftTemplate Template)> SeedAsync(
        ApplicationDbContext context,
        TestTenantContext tenant,
        string start,
        string end)
    {
        context.Restaurants.Add(new Restaurant
        {
            Id = tenant.RestaurantId,
            Name = "Demo",
            Slug = "demo",
            TimeZoneId = "Europe/Belgrade"
        });

        var waiter = new ApplicationUser
        {
            UserName = "demo|marko@example.com",
            Email = "marko@example.com",
            FirstName = "Marko",
            LastName = "Markovic",
            Role = UserRole.Waiter,
            RestaurantId = tenant.RestaurantId
        };

        var template = new ShiftTemplate
        {
            Name = "II smena",
            StartTime = TimeOnly.Parse(start),
            EndTime = TimeOnly.Parse(end)
        };

        context.Users.Add(waiter);
        context.ShiftTemplates.Add(template);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return (waiter, template);
    }

    private static async Task AssignAsync(
        ApplicationDbContext context,
        ApplicationUser waiter,
        ShiftTemplate template,
        WeekDays days,
        DateOnly from,
        DateOnly? to)
    {
        context.ShiftAssignments.Add(new ShiftAssignment
        {
            WaiterId = waiter.Id,
            ShiftTemplateId = template.Id,
            Days = days,
            ValidFrom = from,
            ValidTo = to,
            AssignedByManagerId = ManagerId
        });

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }
}
