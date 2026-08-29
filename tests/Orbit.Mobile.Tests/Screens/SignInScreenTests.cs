using Microsoft.Extensions.Logging.Abstractions;
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
    /// A build with no client id cannot ask Google anything, so it must not offer to - a button that
    /// opens a sheet ending in an error is worse than no button. The same rule Orbit.Web applies when
    /// the server sends no client id.
    /// </summary>
    [Fact]
    public void Google_is_not_offered_when_this_build_has_no_client_id()
    {
        using var context = new ScreenContext();
        context.Google.IsConfigured = false;

        Assert.False(context.Open().CanUseGoogle);
    }

    [Fact]
    public void Google_is_offered_when_the_build_has_one()
    {
        using var context = new ScreenContext();

        Assert.True(context.Open().CanUseGoogle);
    }

    /// <summary>
    /// Closing the sheet is an answer, not a fault. An error message there would tell somebody who
    /// changed their mind that something had gone wrong.
    /// </summary>
    [Fact]
    public async Task Closing_the_Google_sheet_says_nothing()
    {
        using var context = new ScreenContext();
        context.Google.Result = GoogleSignInResult.Cancelled;
        var screen = context.Open();

        await screen.SignInWithGoogleCommand.ExecuteAsync(null);

        Assert.False(screen.HasError);
        Assert.Equal(1, context.Google.RequestCount);
    }

    /// <summary>
    /// A refusal does say something, because there the reader did everything asked of them and still
    /// cannot get in - silence would look like a button that does nothing.
    /// </summary>
    [Fact]
    public async Task A_refused_Google_sign_in_says_so()
    {
        using var context = new ScreenContext();
        context.Google.Result = GoogleSignInResult.Failed;
        var screen = context.Open();

        await screen.SignInWithGoogleCommand.ExecuteAsync(null);

        Assert.True(screen.HasError);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeNotificationServer _notifications = new();
        private readonly FakeEncryptionKeyServer _keys = new();

        private readonly SessionStore _sessionStore = new(new InMemorySessionStorage());

        public FixedGoogleSignIn Google { get; } = new();

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>
        /// Unreachable on purpose: every test here stops before the API is called, and a stub that
        /// answered would hide a call that should not have happened.
        /// </summary>
        private readonly StubHttpMessageHandler _server = StubHttpMessageHandler.Unreachable();

        public SignInViewModel Open()
            => new(
                new AuthenticationClient(_server.ToHttpClient(), FixedNetworkStatus.Online, _sessionStore),
                new OwnEncryptionKeyProvider(
                    new InMemoryChatKeyStorage(), new EncryptionKeyClient(_keys.ToHttpClient()),
                    _sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance),
                new PushRegistration(
                    new FixedDevicePushNotifications(), new NotificationsClient(_notifications.ToHttpClient()),
                    NullLogger<PushRegistration>.Instance),
                _sessionStore,
                new LocalStoreReset(_localStore),
                new Translations(new InMemoryLanguageStore()),
                Google,
                Navigator);

        public void Dispose()
        {
            _notifications.Dispose();
            _keys.Dispose();
            _server.Dispose();
            _localStore.Dispose();
        }
    }
}
