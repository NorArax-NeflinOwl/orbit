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
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

public sealed class CalendarEventEditorTests : TestContext
{
    private static readonly Guid ContactUserId = Guid.NewGuid();
    private static readonly Guid OwnUserId = Guid.NewGuid();
    private static readonly ContactDto Contact =
        new(ContactUserId, "anna", "Anna Kowalska", "anna@example.com", "public-key", DateTimeOffset.UtcNow);

    public CalendarEventEditorTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        Services.AddSingleton(new CalendarApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") }));
        Services.AddSingleton(new GeocodingApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") }));

        var tokenStore = new TokenStore(new StubJSRuntime());
        tokenStore.SetTokenAsync(CreateUnsignedJwt(new Dictionary<string, string>
        {
            ["sub"] = OwnUserId.ToString(),
            ["email"] = "owner@example.com",
            ["name"] = "Test Owner"
        })).GetAwaiter().GetResult();
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(tokenStore);
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
