using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Notifications;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Authentication;

/// <summary>
/// The steps every way in shares. They were written out separately in the sign-in screen once, and the
/// registration screen - the other way in - had lost two of them: it never cleared a local store that
/// belonged to somebody else, and never registered the device for push. The first is the one that
/// mattered, and it is what these hold onto.
/// </summary>
public sealed class SignInCompletionTests : IDisposable
{
    private readonly LocalStore _localStore = new();

    public void Dispose() => _localStore.Dispose();

    /// <summary>
    /// The case a sign-out cannot cover, and the one registering walked straight into: the sign-in
    /// screen reached without one - an expired or revoked session - leaves the previous account's notes,
    /// calendar, contacts and decrypted messages cached for whoever arrives next.
    /// </summary>
    [Fact]
    public async Task Arriving_on_a_phone_somebody_else_used_starts_from_an_empty_store()
    {
        await LeaveBehindANoteOwnedBy(Guid.NewGuid());

        await CompletionFor(Guid.NewGuid()).CompleteAsync(password: null);

        Assert.Empty(await Notes().GetAllAsync());
    }

    [Fact]
    public async Task Coming_back_as_the_same_account_keeps_what_was_cached()
    {
        var sameUser = Guid.NewGuid();
        await LeaveBehindANoteOwnedBy(sameUser);

        await CompletionFor(sameUser).CompleteAsync(password: null);

        Assert.Single(await Notes().GetAllAsync());
    }

    /// <summary>
    /// Google sign-in has no password to open the chat key with. That must not stop the rest: the store
    /// is still cleared and the device still registered, and the key gate asks later.
    /// </summary>
    [Fact]
    public async Task A_way_in_without_a_password_still_completes()
    {
        await LeaveBehindANoteOwnedBy(Guid.NewGuid());

        var completing = CompletionFor(Guid.NewGuid()).CompleteAsync(password: null);

        await completing;
        Assert.True(completing.IsCompletedSuccessfully);
    }

    private LocalNoteRepository Notes() => new(_localStore, TimeProvider.System, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());

    private async Task LeaveBehindANoteOwnedBy(Guid userId)
    {
        await new LocalStoreReset(_localStore).ClearForAsync(userId);
        await Notes().CreateAsync("Theirs", [new NoteContentLineDto("private", false, false)]);
    }

    private SignInCompletion CompletionFor(Guid userId)
    {
        var sessionStore = new SessionStore(new InMemorySessionStorage(
            new UserSession("access", "refresh", userId, "me@orbit.example", "Me")));

        return new SignInCompletion(
            sessionStore,
            new LocalStoreReset(_localStore),
            new OwnEncryptionKeyProvider(
                new InMemoryChatKeyStorage(),
                new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()), sessionStore,
                NullLogger<OwnEncryptionKeyProvider>.Instance),
            new PushRegistration(
                new FixedDevicePushNotifications(),
                new NotificationsClient(new FakeNotificationServer().ToHttpClient()),
                NullLogger<PushRegistration>.Instance));
    }
}
