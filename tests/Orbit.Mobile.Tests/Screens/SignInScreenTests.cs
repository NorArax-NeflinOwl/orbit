using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Config;
using Orbit.Contracts.Users;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Screens.Authentication;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The other way in. Orbit.Web offers Google beside the password form; the phone offers the same
/// choice, and the interesting part is what it does with each of the answers Google can give.
/// </summary>
public sealed class SignInScreenTests
{
    /// <summary>
    /// What the reader types in. Named for the same reason the reset screen's fixtures are: a literal
    /// beside a field called Password is what a secret scanner looks for, and it cannot tell a test from
    /// a credential somebody pasted.
    /// </summary>
    private const string Typed = "sourdough-and-thunder";

    /// <summary>
    /// A deployment with no client id for this app cannot ask Google anything, so the screen must not
    /// offer to - a button that opens a sheet ending in an error is worse than no button. The same rule
    /// Orbit.Web applies when the server sends none.
    /// </summary>
    [Fact]
    public async Task Google_is_not_offered_where_the_deployment_has_no_client_id_for_this_app()
    {
        using var context = new ScreenContext { AndroidClientId = string.Empty };
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.False(screen.IsGoogleOffered);
    }

    [Fact]
    public async Task Google_is_offered_where_the_deployment_has_one()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.IsGoogleOffered);
    }

    /// <summary>
    /// Closing the sheet is an answer, not a fault. An error message there would tell somebody who
    /// changed their mind that something had gone wrong - and nothing may be sent to Orbit either,
    /// since there is no token to send.
    /// </summary>
    [Fact]
    public async Task Closing_the_Google_sheet_says_nothing()
    {
        using var context = new ScreenContext { Callback = null };
        var screen = context.Open();

        await screen.SignInWithGoogleCommand.ExecuteAsync(null);

        Assert.False(screen.HasError);
        Assert.DoesNotContain(context.OrbitRequests, request => request.Uri!.AbsolutePath.EndsWith("/auth/google"));
    }

    /// <summary>
    /// A refusal does say something, because there the reader did everything asked of them and still
    /// cannot get in - silence would look like a button that does nothing.
    /// </summary>
    [Fact]
    public async Task A_Google_sign_in_Orbit_refuses_says_so()
    {
        using var context = new ScreenContext { OrbitAcceptsTheToken = false };
        var screen = context.Open();

        await screen.SignInWithGoogleCommand.ExecuteAsync(null);

        Assert.True(screen.HasError);
    }

    /// <summary>
    /// Where somebody lands when they were sent to the sign-in screen on their way somewhere else - a
    /// tapped notification, or a link Android handed to Orbit. The destination was held rather than
    /// followed, because there was no account to open it in, and only a cold start used to take it: a
    /// tap that arrived at the sign-in screen was quietly lost.
    /// </summary>
    [Fact]
    public async Task Signing_in_goes_on_to_wherever_the_reader_was_heading()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        context.PendingTap.Record("/calendar");

        screen.EmailOrUserName = "someone@orbit.example";
        screen.Password = Typed;
        await screen.SignInCommand.ExecuteAsync(null);

        Assert.Equal("ShowCalendar", context.Navigator.LastDestination);
    }

    [Fact]
    public async Task Signing_in_with_nothing_waiting_opens_the_dashboard()
    {
        using var context = new ScreenContext();
        var screen = context.Open();

        screen.EmailOrUserName = "someone@orbit.example";
        screen.Password = Typed;
        await screen.SignInCommand.ExecuteAsync(null);

        Assert.Equal("ShowDashboard", context.Navigator.LastDestination);
    }

    private sealed class ScreenContext : IDisposable
    {
        private const string AndroidClientIdInUse = "181624005200-example.apps.googleusercontent.com";

        private readonly LocalStore _localStore = new();
        private readonly FakeNotificationServer _notifications = new();
        private readonly FakeEncryptionKeyServer _keys = new();
        private readonly SessionStore _sessionStore = new(new InMemorySessionStorage());

        /// <summary>What the deployment says it has for the Android app, which is what decides the button.</summary>
        public string AndroidClientId { get; init; } = AndroidClientIdInUse;

        /// <summary>What the browser comes back with. Null is a reader who closed the sheet.</summary>
        public IReadOnlyDictionary<string, string>? Callback { get; init; } =
            new Dictionary<string, string> { ["code"] = "the-code" };

        /// <summary>Whether Orbit accepts the identity token Google issued.</summary>
        public bool OrbitAcceptsTheToken { get; init; } = true;

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>What platform code wrote down before there was an account to open it in.</summary>
        public PendingNotificationTap PendingTap { get; } = new();

        public IReadOnlyList<RecordedRequest> OrbitRequests => _orbit.ReceivedRequests;

        private StubHttpMessageHandler _orbit = null!;
        private StubHttpMessageHandler _google = null!;

        public SignInViewModel Open()
        {
            _orbit = StubHttpMessageHandler.Custom((request, _) => Task.FromResult(AnswerAsOrbit(request)));
            _google = StubHttpMessageHandler.RespondingWith(new { id_token = "the-id-token" });

            var authenticationClient = new AuthenticationClient(
                _orbit.ToHttpClient(), FixedNetworkStatus.Online, _sessionStore);

            return new SignInViewModel(
                authenticationClient,
                new GoogleSignIn(new FakeSignInBrowser { Result = Callback }, _google.ToHttpClient()),
                new SignInCompletion(
                    _sessionStore,
                    new LocalStoreReset(_localStore),
                    new OwnEncryptionKeyProvider(
                        new InMemoryChatKeyStorage(), new EncryptionKeyClient(_keys.ToHttpClient()),
                        _sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance),
                    new PushRegistration(
                        new FixedDevicePushNotifications(), new NotificationsClient(_notifications.ToHttpClient()),
                        NullLogger<PushRegistration>.Instance)),
                Openers.AgainstNobody(_localStore, Navigator, PendingTap),
                new Translations(new InMemoryLanguageStore()),
                Navigator);
        }

        private HttpResponseMessage AnswerAsOrbit(HttpRequestMessage request)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/config/client-flags"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ClientFlagsDto(
                        ExceptionDetailsAllowed: false, GoogleClientId: string.Empty, WebAddress: string.Empty,
                        GoogleAndroidClientId: AndroidClientId, GoogleIosClientId: string.Empty))
                };
            }

            if (!OrbitAcceptsTheToken)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            // A sign-in that is accepted answers with a session, whichever way it was made. Without a
            // body the client has nothing to store, which is a failure of this double rather than of
            // the screen - see AuthenticationClient.
            return request.RequestUri.AbsolutePath.Contains("/auth/")
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new AuthResponse(
                        "access", "refresh", Guid.NewGuid(), "someone@orbit.example", "Someone"))
                }
                : new HttpResponseMessage(HttpStatusCode.OK);
        }

        public void Dispose()
        {
            _notifications.Dispose();
            _keys.Dispose();
            _orbit?.Dispose();
            _google?.Dispose();
            _localStore.Dispose();
        }
    }
}
