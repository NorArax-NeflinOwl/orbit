using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Notifications;
using Orbit.Contracts.Users;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

public sealed class CalendarEventEditorTests : OrbitTestContext
{
    private static readonly Guid ContactUserId = Guid.NewGuid();
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly ContactDto Contact =
        new(ContactUserId, "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow,
            RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false);

    public CalendarEventEditorTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        RegisterGoogleIntegrationAccess();
        Services.AddSingleton(new CalendarApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") }));
        Services.AddSingleton(new GeocodingApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") }));
        // CalendarEventEditor.razor fetches notification settings on init - a real (if unreachable)
        // HttpClient like CalendarApiClient/GeocodingApiClient above use would work too (the call is
        // caught and logged, not fatal), but the actual DNS/connect attempt takes real wall-clock time
        // bUnit's synchronous RenderComponent doesn't reliably wait out, unlike a StubHttpMessageHandler's
        // instant in-memory response.
        Services.AddSingleton(new NotificationsApiClient(new HttpClient(
            new StubHttpMessageHandler(_ => JsonResponse(new NotificationSettingsDto(true, true, true, true, true, BannerVisibleSeconds: 5, BannerMinimumGapSeconds: 5))))
        { BaseAddress = new Uri("https://example.test/") }));

        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(CreateUnsignedJwt(new Dictionary<string, string>
        {
            ["sub"] = OwnUserId.ToString(),
            ["email"] = "owner@example.com",
            ["name"] = "Test Owner"
        })).GetAwaiter().GetResult();
        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            tokenStore, new TokenRefreshService(tokenStore, refreshHttpClient));
        // Registered under both the concrete type and the base type it derives from, mirroring
        // Program.cs, so components that inject either one resolve to the same instance.
        Services.AddSingleton(authenticationStateProvider);
        Services.AddSingleton<AuthenticationStateProvider>(authenticationStateProvider);
        Services.AddAuthorizationCore();

        // EncryptedChatMessageSender is only exercised by SaveAsync, which none of these tests reach -
        // it just needs to satisfy CalendarEventEditor's @inject, so its own collaborators are wired
        // with the same dummy-HttpClient pattern used above rather than anything meant to actually run.
        // JSInterop.JSRuntime (bUnit's own JS interop double), not Services.GetRequiredService<IJSRuntime>() -
        // resolving a service from Services here would lock the container against further registrations
        // below, since bUnit treats that as "the component tree has started rendering".
        var jsRuntime = JSInterop.JSRuntime;
        var usersApiClient = new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(usersApiClient);
        var ownEncryptionKeyProvider = new OwnEncryptionKeyProvider(jsRuntime, usersApiClient, authenticationStateProvider);
        var chatApiClientForSender = new ChatApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(new EncryptedChatMessageSender(jsRuntime, ownEncryptionKeyProvider, usersApiClient, chatApiClientForSender));
    }

    /// <summary>
    /// Builds a JWT with a real header and payload but a dummy signature - enough to exercise the
    /// client's own claim-parsing logic, which never checks the signature (the server already did, on
    /// every API call that carries this token).
    /// </summary>
    private static string CreateUnsignedJwt(Dictionary<string, string> claims)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public void Picking_a_contact_adds_them_to_the_guest_list_by_login()
    {
        RegisterChatApiClient([Contact]);

        var cut = RenderComponent<CalendarEventEditor>();
        cut.Find("#guestContactSelect").Change(ContactUserId.ToString());
        cut.Find("#addGuestFromContactButton").Click();

        Assert.Contains("anna", cut.Find("#guestList").TextContent);
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

        Assert.Single(cut.Find("#guestList").Children);
    }

    [Fact]
    public void Removing_a_guest_takes_them_off_the_list()
    {
        RegisterChatApiClient([Contact]);

        var cut = RenderComponent<CalendarEventEditor>();
        cut.Find("#guestContactSelect").Change(ContactUserId.ToString());
        cut.Find("#addGuestFromContactButton").Click();
        cut.Find("#guestList button").Click();

        Assert.Empty(cut.FindAll("#guestList"));
        Assert.Contains("No guests", cut.Markup);
    }

    [Fact]
    public void A_user_with_no_contacts_sees_an_explanatory_message_instead_of_the_picker()
    {
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>();

        Assert.Contains("No contacts", cut.Markup);
        Assert.Empty(cut.FindAll("#guestContactSelect"));
    }

    [Fact]
    public void A_new_event_offers_no_delete()
    {
        // There is nothing to delete yet, and offering it would only lead to a request for an id that
        // does not exist.
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>();

        Assert.DoesNotContain("Delete event", cut.Markup);
    }

    [Fact]
    public void A_new_event_arrives_with_a_reminder_that_will_actually_fire()
    {
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>();

        // A new event used to arrive with no reminder and both channels set to None, so creating one
        // without opening either dropdown could never notify anybody - which reads, fairly, as event
        // reminders being broken. Tasks and inventory items have defaulted to Push all along.
        var reminderSelect = cut.Find("#reminderRows select");
        Assert.Equal("10", reminderSelect.GetAttribute("value"));

        var reminderChannel = cut.Find("#reminderChannelSelect");
        Assert.Equal("Push", reminderChannel.GetAttribute("value"));
    }

    [Fact]
    public void A_reminder_can_be_asked_for_at_the_moment_the_event_begins()
    {
        RegisterChatApiClient([]);

        var cut = RenderComponent<CalendarEventEditor>();

        // The wording for a zero lead time already existed ("starts now"); the picker was the only
        // place it could not be chosen, so an event beginning went unannounced.
        Assert.Contains("When it starts", cut.Find("#reminderRows select").InnerHtml);
    }

    private void RegisterChatApiClient(IReadOnlyList<ContactDto> contacts)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => JsonResponse(contacts))) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new ChatApiClient(httpClient));
    }

    private static HttpResponseMessage JsonResponse(IReadOnlyList<ContactDto> contacts)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(contacts) };

    private static HttpResponseMessage JsonResponse<T>(T body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    /// <summary>
    /// The editor asks whether to offer the Google Calendar link. These tests are not about that link,
    /// so the account the gate sees qualifies for none - mirrors CalendarTests.
    /// </summary>
    private void RegisterGoogleIntegrationAccess()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new AccountDto(
                Guid.NewGuid(), "owner@example.com", "owner", "Owner",
                IsEmailVerified: false, HasPassword: true, IsGoogleLinked: false))
        });
        Services.AddSingleton(new GoogleIntegrationAccess(
            new UsersApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }),
            NullLogger<GoogleIntegrationAccess>.Instance));
    }
}
