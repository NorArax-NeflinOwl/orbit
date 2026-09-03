using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Users;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Authentication;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Making an account from the phone. What matters here is the second password box: a password nobody
/// can read back is a password nobody can check, and one mistyped while making an account locks
/// somebody out of it immediately. Orbit.Web's form has always asked twice.
/// </summary>
public sealed class RegisterScreenTests
{
    /// <inheritdoc cref="SignInScreenTests" path="//summary[1]"/>
    private const string Typed = "sourdough-and-thunder";

    [Fact]
    public async Task Two_passwords_that_differ_are_refused_before_anything_is_sent()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        Fill(screen);
        screen.RepeatedPassword = $"{Typed}-typo";

        await screen.RegisterCommand.ExecuteAsync(null);

        Assert.True(screen.HasError);
        Assert.Empty(context.Registered);
        Assert.Empty(context.Navigator.Destinations);
    }

    /// <summary>Nothing is sent until both boxes are filled: the button is dead rather than hopeful.</summary>
    [Fact]
    public void The_button_waits_for_the_second_box()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        Fill(screen);

        screen.RepeatedPassword = string.Empty;

        Assert.False(screen.RegisterCommand.CanExecute(null));
    }

    [Fact]
    public async Task Two_passwords_that_match_make_the_account()
    {
        using var context = new ScreenContext();
        var screen = context.Open();
        Fill(screen);

        await screen.RegisterCommand.ExecuteAsync(null);

        Assert.False(screen.HasError);
        Assert.Single(context.Registered);
        Assert.Contains(nameof(IScreenNavigator.ShowDashboard), context.Navigator.Destinations);
    }

    private static void Fill(RegisterViewModel screen)
    {
        screen.EmailAddress = "somebody@orbit.example";
        screen.UserName = "somebody";
        screen.DisplayName = "Somebody";
        screen.Password = Typed;
        screen.RepeatedPassword = Typed;
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly SessionStore _sessionStore = new(new InMemorySessionStorage());
        private StubHttpMessageHandler? _orbit;

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>Every account this fake server was asked to make - empty when nothing was sent.</summary>
        public List<string> Registered { get; } = [];

        public RegisterViewModel Open()
        {
            _orbit = StubHttpMessageHandler.Custom((request, _) => Task.FromResult(Answer(request)));
            var httpClient = _orbit.ToHttpClient();

            return new RegisterViewModel(
                new AccountClient(httpClient, FixedNetworkStatus.Online, _sessionStore),
                new SignInCompletion(
                    _sessionStore,
                    new LocalStoreReset(_localStore),
                    new OwnEncryptionKeyProvider(
                        new InMemoryChatKeyStorage(), new EncryptionKeyClient(httpClient), _sessionStore,
                        NullLogger<OwnEncryptionKeyProvider>.Instance),
                    new PushRegistration(
                        new FixedDevicePushNotifications(), new NotificationsClient(httpClient),
                        NullLogger<PushRegistration>.Instance)),
                FixedNetworkStatus.Online,
                new Translations(new InMemoryLanguageStore()),
                Navigator);
        }

        private HttpResponseMessage Answer(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/auth/register", StringComparison.Ordinal))
            {
                Registered.Add(path);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new AuthResponse(
                        "access", "refresh", Guid.NewGuid(), "somebody@orbit.example", "Somebody"))
                };
            }

            // Everything the sign-in that follows a registration touches - a brand-new account has no
            // key backup, so "nothing there" is the honest answer.
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        public void Dispose()
        {
            _orbit?.Dispose();
            _localStore.Dispose();
        }
    }
}
