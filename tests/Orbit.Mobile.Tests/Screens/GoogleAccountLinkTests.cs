using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Config;
using Orbit.Contracts.Users;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Account;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Connecting a Google account to an existing one, and disconnecting it - Orbit.Web's "Google" row on
/// the options page, which the phone had no answer to at all. The journey to Google is the same one
/// signing in takes; what is worth guarding here is what Orbit is told afterwards, and what the reader
/// is told when the server says no.
/// </summary>
public sealed class GoogleAccountLinkTests
{
    private const string AndroidClientId = "181624005200-android.apps.googleusercontent.com";

    /// <summary>
    /// A deployment with no client id for this app cannot connect anything, so the row is absent rather
    /// than a button that could only fail - the sign-in screen hides its Google button for the same reason.
    /// </summary>
    [Fact]
    public async Task It_is_not_offered_where_the_deployment_has_no_client_id()
    {
        using var context = new LinkContext { ClientId = string.Empty };

        var link = await context.ShowAsync(isGoogleLinked: false);

        Assert.False(link.IsOffered);
    }

    [Fact]
    public async Task Connecting_hands_orbit_the_token_google_gave()
    {
        using var context = new LinkContext();
        var link = await context.ShowAsync(isGoogleLinked: false);
        var readAgain = false;
        link.Changed += (_, _) => readAgain = true;

        await link.ConnectCommand.ExecuteAsync(null);

        var sent = Assert.Single(context.OrbitRequests, request =>
            request.Method == HttpMethod.Post && request.Uri!.AbsolutePath.EndsWith("/users/me/google"));
        Assert.Contains("the-id-token", sent.Body);
        Assert.NotEmpty(link.Message);
        // The account is a different one now - what it says about Google, and about having a password.
        Assert.True(readAgain);
    }

    /// <summary>
    /// Closing Google's screen is a choice. Reporting the reader's own decision back to them as a
    /// failure would be wrong, and nothing is sent.
    /// </summary>
    [Fact]
    public async Task Backing_out_of_googles_screen_says_nothing_and_sends_nothing()
    {
        using var context = new LinkContext { Callback = null };
        var link = await context.ShowAsync(isGoogleLinked: false);

        await link.ConnectCommand.ExecuteAsync(null);

        Assert.Empty(link.Message);
        Assert.DoesNotContain(context.OrbitRequests, request => request.Method == HttpMethod.Post);
    }

    /// <summary>
    /// The server's own reason, not a general failure: "already connected to a different Orbit account"
    /// and "set a password first" are two different things for the reader to do next.
    /// </summary>
    [Fact]
    public async Task A_refusal_is_repeated_rather_than_replaced()
    {
        using var context = new LinkContext
        {
            Refusal = "That Google account is already connected to a different Orbit account."
        };

        var link = await context.ShowAsync(isGoogleLinked: false);
        await link.ConnectCommand.ExecuteAsync(null);

        Assert.Equal("That Google account is already connected to a different Orbit account.", link.Message);
    }

    [Fact]
    public async Task Disconnecting_asks_the_server_to_forget_it()
    {
        using var context = new LinkContext();
        var link = await context.ShowAsync(isGoogleLinked: true);

        await link.DisconnectCommand.ExecuteAsync(null);

        var sent = Assert.Single(context.OrbitRequests, request =>
            request.Method == HttpMethod.Delete && request.Uri!.AbsolutePath.EndsWith("/users/me/google"));
        // An endpoint that takes no body is handed none - see AccountClient.SendAsync.
        Assert.Null(sent.Body);
        Assert.NotEmpty(link.Message);
    }

    /// <summary>
    /// Google being the only way in is the case worth saying out loud: the server refuses to disconnect
    /// then, and finding that out by being told no is a worse way to learn it.
    /// </summary>
    [Fact]
    public async Task It_warns_while_google_is_the_only_way_in()
    {
        using var context = new LinkContext { HasPassword = false };

        var link = await context.ShowAsync(isGoogleLinked: true);

        Assert.True(link.IsOnlyWayIn);
    }

    [Fact]
    public async Task With_a_password_to_fall_back_on_there_is_nothing_to_warn_about()
    {
        using var context = new LinkContext();

        var link = await context.ShowAsync(isGoogleLinked: true);

        Assert.False(link.IsOnlyWayIn);
        Assert.False(link.CanConnect);
    }

    /// <summary>A phone with a browser that answers on its own, an Orbit that answers, and no MAUI in sight.</summary>
    private sealed class LinkContext : IDisposable
    {
        private readonly SessionStore _sessionStore = new(new InMemorySessionStorage(
            new UserSession("access", "refresh", Guid.NewGuid(), "me@orbit.example", "Me")));

        private StubHttpMessageHandler? _orbit;
        private StubHttpMessageHandler? _google;

        /// <summary>What the deployment says it has for this app - empty means Google is not offered.</summary>
        public string ClientId { get; init; } = AndroidClientId;

        /// <summary>What Google put in the callback; null for a reader who backed out.</summary>
        public IReadOnlyDictionary<string, string>? Callback { get; init; } =
            new Dictionary<string, string> { ["code"] = "the-code" };

        /// <summary>What the server says no with, or null when it agrees.</summary>
        public string? Refusal { get; init; }

        public bool HasPassword { get; init; } = true;

        public IReadOnlyList<RecordedRequest> OrbitRequests => _orbit!.ReceivedRequests;

        public async Task<GoogleAccountLink> ShowAsync(bool isGoogleLinked)
        {
            _orbit = StubHttpMessageHandler.Custom((request, _) => Task.FromResult(AnswerAsOrbit(request)));
            _google = StubHttpMessageHandler.RespondingWith(new { id_token = "the-id-token" });

            var link = new GoogleAccountLink(
                new AccountClient(_orbit.ToHttpClient(), FixedNetworkStatus.Online, _sessionStore),
                new AuthenticationClient(_orbit.ToHttpClient(), FixedNetworkStatus.Online, _sessionStore),
                new GoogleSignIn(new FakeSignInBrowser { Result = Callback }, _google.ToHttpClient()),
                new Translations(new InMemoryLanguageStore()));

            await link.ShowAsync(new AccountDto(
                Id: Guid.NewGuid(),
                Email: "me@orbit.example", UserName: "me", DisplayName: "Me",
                IsEmailVerified: true, HasPassword: HasPassword, IsGoogleLinked: isGoogleLinked));

            return link;
        }

        private HttpResponseMessage AnswerAsOrbit(HttpRequestMessage request)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/config/client-flags", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ClientFlagsDto(
                        ExceptionDetailsAllowed: false, GoogleClientId: string.Empty, WebAddress: string.Empty,
                        GoogleAndroidClientId: ClientId, GoogleIosClientId: string.Empty))
                };
            }

            // As UserEndpoints.ToLinkResult answers: a refusal carries the reason with it.
            return Refusal is null
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = JsonContent.Create(new { message = Refusal })
                };
        }

        public void Dispose()
        {
            _orbit?.Dispose();
            _google?.Dispose();
        }
    }
}
