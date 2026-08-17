using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Chat;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

public sealed class CalendarEventEditorTests : TestContext
{
    private static readonly Guid ContactUserId = Guid.NewGuid();
    private static readonly ContactDto Contact =
        new(ContactUserId, "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow);

    public CalendarEventEditorTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        Services.AddSingleton(new CalendarApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") }));
        Services.AddSingleton(new GeocodingApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") }));
    }

    [Fact]
    public void Picking_a_contact_appends_their_email_to_the_guest_list()
    {
        RegisterChatApiClient([Contact]);

        var cut = RenderComponent<CalendarEventEditor>();
        cut.Find("#guestContactSelect").Change(ContactUserId.ToString());
        cut.Find("#addGuestFromContactButton").Click();

        Assert.Equal("anna@example.com", cut.Find("#guestsInput").GetAttribute("value"));
    }

    [Fact]
    public void Picking_the_same_contact_twice_does_not_duplicate_the_guest()
    {
        RegisterChatApiClient([Contact]);

        var cut = RenderComponent<CalendarEventEditor>();
        cut.Find("#guestContactSelect").Change(ContactUserId.ToString());
        cut.Find("#addGuestFromContactButton").Click();
        cut.Find("#guestContactSelect").Change(ContactUserId.ToString());
        cut.Find("#addGuestFromContactButton").Click();

        Assert.Equal("anna@example.com", cut.Find("#guestsInput").GetAttribute("value"));
    }

    [Fact]
    public void A_user_with_no_contacts_sees_an_explanatory_message_instead_of_the_picker()
    {
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>();

        Assert.Contains("Brak kontaktów", cut.Markup);
        Assert.Empty(cut.FindAll("#guestContactSelect"));
    }

    private void RegisterChatApiClient(IReadOnlyList<ContactDto> contacts)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(contacts))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new ChatApiClient(httpClient));
    }

    private static HttpResponseMessage JsonResponse(IReadOnlyList<ContactDto> contacts)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(contacts) };
}
