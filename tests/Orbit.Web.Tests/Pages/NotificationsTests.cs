using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Notifications;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Covers what the notifications page is for now that clearing no longer destroys anything: it reads
/// the full record rather than the panel's view, and says which of those entries were cleared away.
/// </summary>
public sealed class NotificationsTests : OrbitTestContext
{
    private readonly List<string> _requestedPaths = [];

    public NotificationsTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void The_page_reads_the_full_record_rather_than_the_panel()
    {
        RegisterApiClients([Entry("A task is overdue")]);

        RenderComponent<Web.Pages.Notifications>();

        // /api/notifications would leave out exactly the entries this page exists to show.
        Assert.Contains("/api/notifications/history", _requestedPaths);
    }

    [Fact]
    public void An_entry_that_was_cleared_is_still_listed_and_labelled()
    {
        RegisterApiClients([Entry("A task is overdue", isDismissed: true)]);

        var cut = RenderComponent<Web.Pages.Notifications>();

        Assert.Contains("A task is overdue", cut.Markup);
        Assert.Contains("Cleared", cut.Find(".notifications-panel-item-title").TextContent);
    }

    [Fact]
    public void An_entry_still_in_the_panel_carries_no_label()
    {
        RegisterApiClients([Entry("A task is overdue")]);

        var cut = RenderComponent<Web.Pages.Notifications>();

        Assert.Empty(cut.FindAll(".notifications-panel-item-tag"));
    }

    [Fact]
    public void The_page_says_how_long_notifications_are_kept()
    {
        // The number comes from the reader's own setting rather than being written into the copy, since
        // Options can change it - see NotificationSettings.RetentionDays.
        RegisterApiClients([Entry("A task is overdue")], retentionDays: 7);

        var cut = RenderComponent<Web.Pages.Notifications>();

        Assert.Contains("last 7 days", cut.Markup);
    }

    [Fact]
    public void One_day_is_not_written_as_days()
    {
        RegisterApiClients([Entry("A task is overdue")], retentionDays: 1);

        var cut = RenderComponent<Web.Pages.Notifications>();

        Assert.Contains("last 1 day,", cut.Markup);
    }

    /// <summary>
    /// Somebody working through a list of notifications is still on that list after answering one of
    /// them, so the page names itself as the way back - see ReturnTo. Until 2026-09-10 an entry opened
    /// here finished on whichever section the thing belongs to, and the rest of the list had to be found
    /// again.
    /// </summary>
    [Fact]
    public void Opening_an_entry_says_to_come_back_to_this_page()
    {
        RegisterApiClients([Entry("A task is overdue")]);

        var cut = RenderComponent<Web.Pages.Notifications>();
        cut.Find(".notifications-panel-item-link").Click();

        Assert.EndsWith(
            "/tasks/1?returnTo=%2Fnotifications",
            Services.GetRequiredService<NavigationManager>().Uri);
    }

    /// <summary>
    /// A shared place is met on the map rather than on a page of its own, so its address already carries
    /// a query - and a second "?" would have made the place id read as "{id}?returnTo=..." and opened
    /// the map on no pin.
    /// </summary>
    [Fact]
    public void An_entry_whose_address_already_has_a_query_keeps_it()
    {
        RegisterApiClients([Entry("A place was shared with you", url: "/map?place=7")]);

        var cut = RenderComponent<Web.Pages.Notifications>();
        cut.Find(".notifications-panel-item-link").Click();

        Assert.EndsWith(
            "/map?place=7&returnTo=%2Fnotifications",
            Services.GetRequiredService<NavigationManager>().Uri);
    }

    private void RegisterApiClients(IReadOnlyList<NotificationEntryDto> entries, int retentionDays = 3)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            _requestedPaths.Add(path);

            if (path.EndsWith("/settings", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new NotificationSettingsDto(
                        true, true, true, true, ShowExceptionDetails: false,
                        BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5, AllowShareNotifications: false,
                        RetentionDays: retentionDays))
                };
            }

            if (path.EndsWith("/client-flags", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { exceptionDetailsAllowed = false })
                };
            }

            return request.Method == HttpMethod.Post
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(entries) };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new NotificationsApiClient(httpClient));
        Services.AddSingleton(new ClientFlagsApiClient(httpClient));
        Services.AddSingleton(new NotificationFeedState());
        Services.AddSingleton(new ClientExceptionLog(new StubJSRuntime(), NullLogger<ClientExceptionLog>.Instance));
    }

    private static NotificationEntryDto Entry(string title, bool isDismissed = false, string url = "/tasks/1")
        => new(
            Guid.NewGuid(), "PushReminder", title, "Body", url, DateTimeOffset.UtcNow,
            IsRead: isDismissed, IsDismissed: isDismissed);
}
