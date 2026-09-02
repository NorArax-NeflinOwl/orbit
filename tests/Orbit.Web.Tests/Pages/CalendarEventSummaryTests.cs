using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Users;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// An appointment read rather than filled in: when, where, what it is about, who is coming. The form is
/// one named press further in, the same shape a task list and a storage settled on.
/// </summary>
public sealed class CalendarEventSummaryTests : OrbitTestContext
{
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid GuestId = Guid.NewGuid();

    private CalendarEventDto _event = AnEvent();

    public CalendarEventSummaryTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        // Leaflet is a real map in a real browser; here it only has to be reachable - see PlaceMap.
        JSInterop.SetupModule("./js/locationMap.js").SetupVoid("showLocation", _ => true);
        var httpClient = new HttpClient(new StubHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath.EndsWith("/chat/contacts", StringComparison.Ordinal)
                ? JsonContent.Create(new[]
                {
                    new ContactDto(
                        GuestId, "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
                        RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false)
                })
                : JsonContent.Create(_event)
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new CalendarApiClient(httpClient));
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new UsersApiClient(httpClient));
        RegisterGoogleIntegrationAccess();
    }

    [Fact]
    public void An_appointment_says_when_where_what_it_is_about_and_who_is_coming()
    {
        var cut = RenderComponent<CalendarEventSummary>(parameters => parameters.Add(page => page.Id, EventId));

        var card = cut.Find(".card").TextContent;
        Assert.Contains("14.09.2026", card);
        Assert.Contains("Przychodnia", card);
        Assert.Contains("Bring the x-rays", card);
        Assert.Contains("anna", card);
        // The lead times it will actually speak at, longest first, with "at the start" folded in.
        Assert.Contains("1 hr before", card);
        Assert.Contains("at the start", card);
    }

    /// <summary>A place that is known is a place worth drawing - see PlaceMap.</summary>
    [Fact]
    public void A_place_with_a_pin_is_drawn()
    {
        var cut = RenderComponent<CalendarEventSummary>(parameters => parameters.Add(page => page.Id, EventId));

        Assert.NotEmpty(cut.FindAll("#calendarEventMap"));
    }

    [Fact]
    public void The_form_is_a_named_press()
    {
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<CalendarEventSummary>(parameters => parameters.Add(page => page.Id, EventId));

        cut.Find(".page-header-actions .overflow-menu-trigger").Click();
        cut.FindAll(".avatar-dropdown-item").First(entry => entry.TextContent.Trim() == "Edit").Click();

        Assert.EndsWith($"/calendar/{EventId}/edit", navigationManager.Uri);
    }

    /// <summary>Somebody else's appointment is theirs to change: this reader looks, and cannot delete.</summary>
    [Fact]
    public void An_event_shared_with_this_reader_offers_no_deleting()
    {
        _event = AnEvent() with { IsShared = true, SharedByUserName = "Anna", AccessLevel = "ReadOnly" };

        var cut = RenderComponent<CalendarEventSummary>(parameters => parameters.Add(page => page.Id, EventId));
        cut.Find(".page-header-actions .overflow-menu-trigger").Click();

        var offered = cut.FindAll(".avatar-dropdown-item").Select(entry => entry.TextContent.Trim()).ToList();
        Assert.Contains("View", offered);
        Assert.DoesNotContain("Delete", offered);
    }

    private static CalendarEventDto AnEvent()
        => new(
            EventId,
            new CalendarEventDetailsDto(
                "Dentist", "Bring the x-rays",
                new EventLocationDto("Przychodnia, ul. Długa 4", 52.23, 21.01), null,
                new DateTimeOffset(new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Local)),
                new DateTimeOffset(new DateTime(2026, 9, 14, 11, 0, 0, DateTimeKind.Local)),
                IsAllDay: false, Recurrence: null, Guests: [GuestId],
                ReminderMinutesBeforeStart: [60], ReminderNotificationChannel: "Push", Priority: "Normal",
                NotifyAtStart: true),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    /// <summary>
    /// The page injects this to decide whether to offer the Google link. Stubbed over an account that
    /// qualifies for none: these tests are not about that link, and a real HttpClient here would spend
    /// wall-clock time on a DNS lookup bUnit's synchronous render does not wait out.
    /// </summary>
    private void RegisterGoogleIntegrationAccess()
    {
        var usersHttpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new AccountDto(
                Guid.NewGuid(), "user@example.com", "user", "User",
                IsEmailVerified: false, HasPassword: true, IsGoogleLinked: false))
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new GoogleIntegrationAccess(
            new UsersApiClient(usersHttpClient), new DevicePreferences(new StubJSRuntime()),
            NullLogger<GoogleIntegrationAccess>.Instance));
    }
}
