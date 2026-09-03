using Orbit.Mobile.Authentication;

namespace Orbit.Mobile.Crypto;

/// <summary>
/// The way in to sealing and opening private notes, lists and warehouses on this device - the
/// counterpart of Orbit.Web's class of the same name, and sealed under the same key, so a private note
/// written in a browser opens on the phone and one written here opens in the browser.
///
/// The key is the one chat already uses, agreed with the owner's own public key on both sides (see
/// <see cref="ChatIdentity.EncryptForSelf"/>). That means no second key to generate, back up or
/// restore: a device that can read your chat can read your private notes, one that cannot is sent to
/// the same key gate, and a password reset that replaces the key pair loses both alike.
///
/// Reads the key this device already holds and never fetches, restores or generates one - that is
/// <see cref="OwnEncryptionKeyProvider"/>'s job, and it needs both the network and the account
/// password. Using a key that is already here needs neither, which is what lets a private note be read
/// and written with no connection at all.
///
/// Unlocking hands back a <see cref="PrivateContentKey"/> rather than sealing item by item, because a
/// list opens every private row it holds at once: going to the phone's keystore per row would make the
/// notes screen pay for it as many times as there are notes. EncryptedChatMessageReader opens a
/// conversation the same way.
/// </summary>
public sealed class PrivateContentSealer
{
    private readonly IChatKeyStorage _keyStorage;
    private readonly SessionStore _sessionStore;

    public PrivateContentSealer(IChatKeyStorage keyStorage, SessionStore sessionStore)
    {
        _keyStorage = keyStorage;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Whether this device could seal or open anything at all, without opening the key. What a screen
    /// asks to tell "you have no key here" from "this was sealed under a key pair since replaced" - two
    /// situations that look identical from the sealed content alone and have different answers.
    /// </summary>
    public async Task<bool> HasKeyAsync(CancellationToken cancellationToken = default)
        => await FindPrivateKeyJwkAsync(cancellationToken) is not null;

    /// <exception cref="EncryptionKeyLockedException">
    /// This device holds no key for the signed-in user. Callers turn that into the same "unlock to
    /// continue" prompt chat uses - sealing under a key that is not there would produce content nobody
    /// could ever open.
    /// </exception>
    public async Task<PrivateContentKey> UnlockAsync(CancellationToken cancellationToken = default)
    {
        if (await FindPrivateKeyJwkAsync(cancellationToken) is not { } privateKeyJwk)
        {
            throw new EncryptionKeyLockedException();
        }

        return new PrivateContentKey(ChatIdentity.FromPrivateKeyJwk(privateKeyJwk));
    }

    /// <summary>
    /// Keyed by user, so a phone that has had two accounts on it never opens one's private notes with
    /// the other's key. Nobody signed in means no key, which reads the same as not holding one.
    /// </summary>
    private async Task<string?> FindPrivateKeyJwkAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _sessionStore.GetAsync() is { } session
            ? await _keyStorage.ReadPrivateKeyJwkAsync(session.UserId)
            : null;
    }
}
