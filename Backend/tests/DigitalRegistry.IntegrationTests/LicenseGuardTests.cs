using DigitalRegistry.Application.Features.Auth.Commands.Login;
using DigitalRegistry.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DigitalRegistry.IntegrationTests;

/// <summary>
/// What the till does once the licence lapses.
/// </summary>
/// <remarks>
/// Its own factory, because the licence has to be moved into the past and that would leave every
/// other test working against a venue that cannot trade.
/// </remarks>
public class LicenseGuardTests : IClassFixture<LicenseGuardTests.ExpiredLicenceFactory>
{
    public class ExpiredLicenceFactory : DigitalRegistryApiFactory;

    private readonly ExpiredLicenceFactory factory;

    public LicenseGuardTests(ExpiredLicenceFactory factory) => this.factory = factory;

    private async Task ExpireLicenceAsync() =>
        await factory.MutateAsync(async context =>
        {
            var licence = await context.Licenses.IgnoreQueryFilters().SingleAsync();

            // Backdated rather than marked Expired: the status is derived from the date, which is
            // the whole point of the design — nothing has to run overnight to make a licence lapse.
            licence.ExpiresAtUtc = DateTime.UtcNow.AddDays(-1);
        });

    [Theory]
    [InlineData("/api/floor-plan")]
    [InlineData("/api/menu")]
    [InlineData("/api/tables/availability?partySize=2&fromUtc=2026-01-01T10:00:00Z&toUtc=2026-01-01T12:00:00Z")]
    [InlineData("/api/reports/turnover?from=2026-01-01&to=2026-01-01")]
    public async Task An_expired_licence_stops_every_till_call(string route)
    {
        await ExpireLicenceAsync();

        var owner = await factory.SignInAsync(DigitalRegistryApiFactory.OwnerEmail);
        var response = await owner.GetAsync(route);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        // 402 rather than 401 or 403: the caller is who they say they are and would be allowed to do
        // this. The client keys off the code to show the renewal screen instead of a sign-in error.
        Assert.Equal("LICENSE_EXPIRED", problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Signing_in_and_reading_the_licence_still_work()
    {
        await ExpireLicenceAsync();

        var client = factory.CreateClient();

        // Otherwise the person is locked out of the one screen that explains why they are locked out.
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginCommand(
                DigitalRegistryApiFactory.RestaurantSlug,
                DigitalRegistryApiFactory.OwnerEmail,
                DigitalRegistryApiFactory.DemoPassword));

        login.EnsureSuccessStatusCode();

        var owner = await factory.SignInAsync(DigitalRegistryApiFactory.OwnerEmail);
        var status = await owner.GetAsync("/api/license/status");

        status.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Renewing_lets_the_till_straight_back_in()
    {
        await ExpireLicenceAsync();

        var owner = await factory.SignInAsync(DigitalRegistryApiFactory.OwnerEmail);

        Assert.Equal(HttpStatusCode.PaymentRequired, (await owner.GetAsync("/api/floor-plan")).StatusCode);

        await factory.MutateAsync(async context =>
        {
            var licence = await context.Licenses.IgnoreQueryFilters().SingleAsync();
            licence.Renew(LicensePlan.Monthly, licence.IssuedByAdminId, DateTime.UtcNow);
        });

        // No cache to invalidate and no restart: the guard asks the database on every request,
        // because the master application that takes the payment is a different process.
        (await owner.GetAsync("/api/floor-plan")).EnsureSuccessStatusCode();
    }
}
