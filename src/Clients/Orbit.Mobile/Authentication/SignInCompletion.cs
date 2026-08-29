using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Notifications;

namespace Orbit.Mobile.Authentication;

/// <summary>
/// Everything that has to happen once the server has accepted somebody, in the order it has to happen
/// in. Shared by every way in - password, Google, and registering - because the steps are about the
/// session rather than about how it was obtained, and the one that had them written out separately had
/// already lost two of them.
///
/// What that cost: registering on a phone somebody else had been signed into left their notes, calendar,
/// contacts and decrypted messages in the local database for the new account to read, because only the
/// sign-in path cleared it. Signing out clears the store, so this only bites where the sign-in screen is
/// reached without one - a session that expired or was revoked, which is exactly the case
/// <see cref="LocalStoreReset"/> exists for. Registering also never registered the device for push.
/// </summary>
public sealed class SignInCompletion
{
    private readonly SessionStore _sessionStore;
    private readonly LocalStoreReset _localStore;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly PushRegistration _pushRegistration;

    public SignInCompletion(
        SessionStore sessionStore, LocalStoreReset localStore,
        OwnEncryptionKeyProvider encryptionKeyProvider, PushRegistration pushRegistration)
    {
        _sessionStore = sessionStore;
        _localStore = localStore;
        _encryptionKeyProvider = encryptionKeyProvider;
        _pushRegistration = pushRegistration;
    }

    /// <param name="password">
    /// The plaintext password, or null for a way in that has none - Google. Only a password can open the
    /// chat key (see <see cref="OwnEncryptionKeyProvider"/>), so without one the key stays locked and the
    /// key gate asks for it when chat is opened.
    /// </param>
    public async Task CompleteAsync(string? password, CancellationToken cancellationToken = default)
    {
        // First, and before anything reads the local database: whatever is cached belongs to whoever was
        // signed in last, and this may not be them.
        if (await _sessionStore.GetAsync() is { } session)
        {
            await _localStore.ClearIfSomebodyElsesAsync(session.UserId, cancellationToken);
        }

        // The one moment the plaintext password exists. Best-effort: failing here leaves chat locked,
        // which the reader can recover from, and must not fail a sign-in the server already accepted.
        if (password is { Length: > 0 })
        {
            await TryUnlockChatKeyAsync(password, cancellationToken);
        }

        // Every time, not just the first: a push token changes when the app is reinstalled or its data
        // cleared, and the old one stops working without saying so. Best-effort for the same reason -
        // push is an addition to the in-app feed, never a reason to fail a sign-in.
        await _pushRegistration.RegisterThisDeviceAsync(cancellationToken);
    }

    private async Task TryUnlockChatKeyAsync(string password, CancellationToken cancellationToken)
    {
        try
        {
            await _encryptionKeyProvider.UnlockOrCreateAsync(password, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not unlock the chat key after signing in: {exception}");
        }
    }
}
