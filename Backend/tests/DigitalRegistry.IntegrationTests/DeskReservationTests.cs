using DigitalRegistry.Application.Features.FloorPlan;
using DigitalRegistry.Application.Features.Reservations;
using DigitalRegistry.Application.Features.Reservations.Commands.CreateReservation;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace DigitalRegistry.IntegrationTests;

/// <summary>
/// Bookings taken by the desk, on behalf of somebody with no account.
/// </summary>
/// <remarks>
/// Nearly every booking a restaurant takes arrives by telephone from a guest who will never have an
/// account. The API used to book for whoever was calling it, so such a booking would have gone down
/// in the name of the waiter who answered the phone — wrong on the service sheet, and wrong about
/// whose booking it is to cancel. What the desk gives instead is a name, and the booking belongs to
/// the venue.
/// </remarks>
public class DeskReservationTests : IClassFixture<DigitalRegistryApiFactory>
{
    private readonly DigitalRegistryApiFactory factory;

    public DeskReservationTests(DigitalRegistryApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task A_booking_taken_by_the_desk_is_filed_under_the_guests_name_not_the_waiters()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);
        var table = await FirstTableAsync(waiter);

        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(19);

        var response = await waiter.PostAsJsonAsync(
            "/api/reservations",
            new CreateReservationCommand(
                table.Id,
                start,
                start.AddHours(2),
                PartySize: 2,
                ContactName: "  Marko Marković  ",
                ContactPhone: " 060 111 222 "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = (await response.Content.ReadFromJsonAsync<ReservationDto>())!;

        // The row is what matters here: the response repeats the period, but says nothing about
        // whose booking it is, which is the whole point of the change.
        var stored = await factory.QueryAsync(context => context.Reservations
            .IgnoreQueryFilters()
            .SingleAsync(reservation => reservation.Id == created.Id));

        Assert.Null(stored.GuestId);
        Assert.Equal("Marko Marković", stored.ContactName);
        Assert.Equal("060 111 222", stored.ContactPhone);
        Assert.NotNull(stored.TakenByUserId);

        // The service sheet reads the written name, and says who took the booking beside it.
        var sheet = await waiter.GetFromJsonAsync<List<ReservationScheduleEntryDto>>(
            $"/api/reservations/schedule?date={start:yyyy-MM-dd}");

        var entry = sheet!.Single(row => row.Id == created.Id);

        Assert.Equal("Marko Marković", entry.GuestName);
        Assert.Equal("060 111 222", entry.ContactPhone);
        Assert.Null(entry.GuestId);
        Assert.NotNull(entry.TakenBy);
    }

    [Fact]
    public async Task Staff_who_give_no_name_are_refused_rather_than_booking_it_for_themselves()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);
        var table = await FirstTableAsync(waiter);

        var start = DateTime.UtcNow.Date.AddDays(4).AddHours(19);

        var response = await waiter.PostAsJsonAsync(
            "/api/reservations",
            new CreateReservationCommand(table.Id, start, start.AddHours(2), PartySize: 2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_guest_cannot_book_in_somebody_elses_name()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);
        var guest = await factory.SignInAsync(DigitalRegistryApiFactory.GuestEmail);
        var table = await FirstTableAsync(manager);

        var start = DateTime.UtcNow.Date.AddDays(5).AddHours(19);

        var response = await guest.PostAsJsonAsync(
            "/api/reservations",
            new CreateReservationCommand(
                table.Id,
                start,
                start.AddHours(2),
                PartySize: 2,
                ContactName: "Neko Drugi"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_guest_booking_for_themselves_still_goes_on_their_own_account()
    {
        var manager = await factory.SignInAsync(DigitalRegistryApiFactory.ManagerEmail);
        var guest = await factory.SignInAsync(DigitalRegistryApiFactory.GuestEmail);
        var table = await FirstTableAsync(manager);

        var start = DateTime.UtcNow.Date.AddDays(6).AddHours(19);

        var response = await guest.PostAsJsonAsync(
            "/api/reservations",
            new CreateReservationCommand(table.Id, start, start.AddHours(2), PartySize: 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = (await response.Content.ReadFromJsonAsync<ReservationDto>())!;

        var stored = await factory.QueryAsync(context => context.Reservations
            .IgnoreQueryFilters()
            .SingleAsync(reservation => reservation.Id == created.Id));

        Assert.NotNull(stored.GuestId);
        Assert.Null(stored.ContactName);
        Assert.Null(stored.TakenByUserId);

        // And it is still theirs to see and to cancel.
        var mine = await guest.GetFromJsonAsync<List<ReservationDto>>("/api/reservations/mine");

        Assert.Contains(mine!, reservation => reservation.Id == created.Id);
    }

    [Fact]
    public async Task A_desk_booking_is_not_the_desks_to_see_as_a_guest()
    {
        var waiter = await factory.SignInAsync(DigitalRegistryApiFactory.WaiterEmail);
        var guest = await factory.SignInAsync(DigitalRegistryApiFactory.GuestEmail);
        var table = await FirstTableAsync(waiter);

        var start = DateTime.UtcNow.Date.AddDays(7).AddHours(19);

        var response = await waiter.PostAsJsonAsync(
            "/api/reservations",
            new CreateReservationCommand(
                table.Id,
                start,
                start.AddHours(2),
                PartySize: 2,
                ContactName: "Ana Anić"));

        var created = (await response.Content.ReadFromJsonAsync<ReservationDto>())!;

        // It belongs to no account, so no guest's "my reservations" may claim it.
        var mine = await guest.GetFromJsonAsync<List<ReservationDto>>(
            "/api/reservations/mine?includePast=true");

        Assert.DoesNotContain(mine!, reservation => reservation.Id == created.Id);

        var byId = await guest.GetAsync($"/api/reservations/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);
    }

    private static async Task<FloorPlanTableDto> FirstTableAsync(HttpClient client)
    {
        var plan = await client.GetFromJsonAsync<FloorPlanDto>("/api/floor-plan");

        return plan!.Rooms.SelectMany(room => room.Tables).First();
    }
}
