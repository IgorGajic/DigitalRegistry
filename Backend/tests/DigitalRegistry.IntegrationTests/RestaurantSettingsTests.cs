using DigitalRegistry.Application.Features.Settings;
using DigitalRegistry.Domain.Enums;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DigitalRegistry.IntegrationTests;

/// <summary>
/// Who may repaint the till, and who merely has to know what colour it is.
/// </summary>
/// <remarks>
/// The two halves of this endpoint are guarded differently, and the split is the point: the theme
/// decides the colours the floor screen draws table states in, so every member of staff has to be
/// able to read it, while choosing it is a decision made once for the whole venue.
/// </remarks>
public class RestaurantSettingsTests : IClassFixture<RestaurantSettingsTests.SettingsFactory>
{
    /// <summary>Its own factory: these tests repaint the venue, which would follow other tests around.</summary>
    public class SettingsFactory : DigitalRegistryApiFactory;

    private readonly SettingsFactory factory;

    public RestaurantSettingsTests(SettingsFactory factory) => this.factory = factory;

    [Fact]
    public async Task A_venue_starts_in_the_palette_the_till_has_always_had()
    {
        var owner = await factory.SignInAsync(DigitalRegistryApiFactory.OwnerEmail);

        var settings = await owner.GetFromJsonAsync<RestaurantSettingsDto>("/api/settings");

        Assert.NotNull(settings);
        Assert.Equal(AppTheme.Petrol, settings.Theme);
        Assert.False(string.IsNullOrWhiteSpace(settings.RestaurantName));
    }

    [Fact]
    public async Task The_owner_repaints_the_venue_and_it_stays_repainted()
    {
        var owner = await factory.SignInAsync(DigitalRegistryApiFactory.OwnerEmail);

        var response = await owner.PutAsJsonAsync("/api/settings/theme", new { theme = AppTheme.Forest });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<RestaurantSettingsDto>();
        Assert.Equal(AppTheme.Forest, updated!.Theme);

        // Read back on a fresh request, because the answer that matters is the one the next person
        // to open the till gets, not the one the write happened to echo.
        var reread = await owner.GetFromJsonAsync<RestaurantSettingsDto>("/api/settings");
        Assert.Equal(AppTheme.Forest, reread!.Theme);

        await owner.PutAsJsonAsync("/api/settings/theme", new { theme = AppTheme.Petrol });
    }

    [Fact]
    public async Task A_waiter_may_read_the_theme_because_the_floor_screen_is_drawn_in_it()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);

        var response = await waiter.GetAsync("/api/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_waiter_may_not_repaint_the_venue()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);

        var response = await waiter.PutAsJsonAsync("/api/settings/theme", new { theme = AppTheme.Charcoal });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Neither_may_a_manager__this_is_the_owner_s_room()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);

        var response = await manager.PutAsJsonAsync("/api/settings/theme", new { theme = AppTheme.Sand });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_theme_that_does_not_exist_is_refused_rather_than_stored()
    {
        var owner = await factory.SignInAsync(DigitalRegistryApiFactory.OwnerEmail);

        // Every theme has had its table-state colours and its chart palette checked against its own
        // surface. A number outside the set is a till drawn in nothing, so it is turned away here
        // rather than left for the client to fall back from.
        var response = await owner.PutAsJsonAsync("/api/settings/theme", new { theme = 99 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var settings = await owner.GetFromJsonAsync<RestaurantSettingsDto>("/api/settings");
        Assert.Equal(AppTheme.Petrol, settings!.Theme);
    }
}
