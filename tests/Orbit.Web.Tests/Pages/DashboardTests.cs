using System.Net;
using System.Net.Http.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

public sealed class DashboardTests : TestContext
{
    public DashboardTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        // Notes/task lists/events aren't what these tests exercise - each is stubbed to an empty list so
        // the dashboard finishes loading without depending on unrelated fixture data.
        RegisterEmptyNotesApiClient();
        RegisterEmptyTasksApiClient();
        RegisterEmptyCalendarApiClient();
    }

    [Fact]
    public void A_contact_pending_approval_from_the_other_party_shows_only_in_the_chats_column()
    {
        var approvedContact = new ContactDto(
            Guid.NewGuid(), "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false);
        var pendingContact = new ContactDto(
            Guid.NewGuid(), "bartek", "Bartek Nowak", "bartek@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: true);
        RegisterChatApiClient([approvedContact, pendingContact]);

        var cut = RenderComponent<Dashboard>();

        var chatsColumnText = FindColumn(cut, "Czaty").TextContent;
        var contactsColumnText = FindColumn(cut, "Kontakty").TextContent;
        Assert.Contains("Anna Kowalska", chatsColumnText);
        Assert.Contains("Bartek Nowak", chatsColumnText);
        Assert.Contains("Anna Kowalska", contactsColumnText);
        Assert.DoesNotContain("Bartek Nowak", contactsColumnText);
    }

    private static IElement FindColumn(IRenderedComponent<Dashboard> cut, string heading)
        => cut.FindAll("div.dashboard-column").Single(column => column.QuerySelector("h2")!.TextContent == heading);

    private void RegisterChatApiClient(IReadOnlyList<ContactDto> contacts)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(contacts))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new ChatApiClient(httpClient));
    }

    private void RegisterEmptyNotesApiClient()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(Array.Empty<NoteDto>()))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new NotesApiClient(httpClient));
    }

    private void RegisterEmptyTasksApiClient()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(Array.Empty<TaskDto>()))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new TasksApiClient(httpClient));
    }

    private void RegisterEmptyCalendarApiClient()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(Array.Empty<CalendarEventDto>()))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new CalendarApiClient(httpClient));
    }

    private static HttpResponseMessage JsonResponse<TItem>(IReadOnlyList<TItem> items)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(items) };
}
