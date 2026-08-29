using Microsoft.Extensions.Logging;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;

namespace Orbit.Mobile.Crypto;

/// <summary>What happened when the app tried to get this device a usable chat key.</summary>
public enum EncryptionKeyOutcome
{
    /// <summary>The device now holds the account's existing key - restored, or already present.</summary>
    Unlocked,

    /// <summary>
    /// A brand-new key was generated, which is only ever done when the server confirmed the account has
    /// no backup at all. Nothing was lost, because there was nothing to lose.
    /// </summary>
    Created,

    /// <summary>
    /// No usable key, and none was invented. Chat stays locked until the user tries again with a
    /// connection, or with the password the backup was actually wrapped under.
    /// </summary>
    StillLocked
}

/// <summary>
/// Gets this device a usable chat key, and keeps the server's password-wrapped backup of it current.
/// The mobile counterpart of Orbit.Web's OwnEncryptionKeyProvider, and deliberately the same shape.
///
/// The split between the two entry points is the important part, and is taken straight from the web:
/// <see cref="EnsurePublicKeyAsync"/> never creates or restores anything, because doing that without the
/// password would either orphan an existing backup or produce a key nobody can ever back up. Only
/// <see cref="UnlockOrCreateAsync"/> may, and it is called right after signing in or registering, while
/// the plaintext password is still on hand - the only moment it ever is.
///
/// <b>Two deliberate departures from the web client</b>, both the same principle: never replace a key
/// unless the server has confirmed there is nothing to replace.
///
/// 1. Orbit.Web treats a failed backup lookup as "no backup exists" and generates a fresh key, reasoning
///    that a sign-in should never leave a browser locked out. On a phone, losing the network mid-sign-in
///    is ordinary rather than rare, so the same rule would silently discard the user's real key - and
///    with it every message ever sent to them - for the length of a tunnel. Here a lookup that could not
///    be made leaves the key locked, which is recoverable; generating is not.
/// 2. Likewise when a backup exists but this password cannot open it. That means it was wrapped under an
///    older password, and the key it protects is still the real one. Generating would publish a new
///    public key and make the old backup permanently useless on every device.
/// </summary>
public sealed class OwnEncryptionKeyProvider
{
    private readonly IChatKeyStorage _keyStorage;
    private readonly EncryptionKeyClient _encryptionKeyClient;
    private readonly SessionStore _sessionStore;
    private readonly ILogger<OwnEncryptionKeyProvider> _logger;

    public OwnEncryptionKeyProvider(
        IChatKeyStorage keyStorage, EncryptionKeyClient encryptionKeyClient, SessionStore sessionStore,
        ILogger<OwnEncryptionKeyProvider> logger)
    {
        _keyStorage = keyStorage;
        _encryptionKeyClient = encryptionKeyClient;
        _sessionStore = sessionStore;
        _logger = logger;
    }

    /// <summary>
    /// The identity to encrypt and decrypt with. Never generates one - see the class remarks.
    /// </summary>
    /// <exception cref="EncryptionKeyLockedException">This device holds no key for the signed-in user.</exception>
    public async Task<ChatIdentity> OpenAsync(CancellationToken cancellationToken = default)
    {
        var userId = await RequireSignedInUserIdAsync();
        if (await _keyStorage.ReadPrivateKeyJwkAsync(userId) is not { } privateKeyJwk)
        {
            throw new EncryptionKeyLockedException();
        }

        return ChatIdentity.FromPrivateKeyJwk(privateKeyJwk);
    }

    /// <summary>
    /// Whether this device already holds a key, without opening it. What the gate asks before deciding
    /// whether to ask for a password at all.
    /// </summary>
    public async Task<bool> HasKeyAsync(CancellationToken cancellationToken = default)
        => await _keyStorage.ReadPrivateKeyJwkAsync(await RequireSignedInUserIdAsync()) is not null;

    /// <summary>The signed-in user's public key, as other people need it to reach them.</summary>
    /// <exception cref="EncryptionKeyLockedException">This device holds no key for the signed-in user.</exception>
    public async Task<string> EnsurePublicKeyAsync(CancellationToken cancellationToken = default)
    {
        using var identity = await OpenAsync(cancellationToken);
        return identity.PublicKeyBase64;
    }

    /// <summary>
    /// Called right after signing in or registering, while the password is still available. Restores the
    /// account's existing key when there is one, and generates a key only when the server has said there
    /// is no backup at all.
    /// </summary>
    public async Task<EncryptionKeyOutcome> UnlockOrCreateAsync(
        string password, CancellationToken cancellationToken = default)
    {
        var userId = await RequireSignedInUserIdAsync();

        // A device that already has the key keeps it. Backing it up again covers the case of a device
        // that predates this feature, or whose backup was written under an older password.
        if (await _keyStorage.ReadPrivateKeyJwkAsync(userId) is { } existingJwk)
        {
            using var existing = ChatIdentity.FromPrivateKeyJwk(existingJwk);
            await TryPublishBackupAsync(existing, password, cancellationToken);
            return EncryptionKeyOutcome.Unlocked;
        }

        return await RestoreOrCreateAsync(userId, password, cancellationToken);
    }

    /// <summary>
    /// Re-wraps the server-side backup under a new password, which a password change <b>must</b> do or
    /// the backup silently stops being openable: it stays wrapped under the old password, so the next
    /// device to restore it fails, generates a fresh key instead, and every earlier message becomes
    /// unreadable there. Only a client can do this - the server never sees the private key.
    ///
    /// Returns what happened rather than throwing, so a password change that already succeeded is not
    /// reported as a failure; the caller decides how loudly to say that the backup is now stale.
    /// </summary>
    public async Task<EncryptionKeyOutcome> RewrapAsync(
        string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var userId = await RequireSignedInUserIdAsync();

        // Prefer this device's own key; otherwise restore with the password the user just proved they
        // know, which covers changing the password on a device that never had the key.
        if (await _keyStorage.ReadPrivateKeyJwkAsync(userId) is not { } privateKeyJwk)
        {
            var restored = await RestoreOrCreateAsync(userId, currentPassword, cancellationToken);
            if (restored is EncryptionKeyOutcome.StillLocked)
            {
                return EncryptionKeyOutcome.StillLocked;
            }

            privateKeyJwk = (await _keyStorage.ReadPrivateKeyJwkAsync(userId))!;
        }

        using var identity = ChatIdentity.FromPrivateKeyJwk(privateKeyJwk);
        await _encryptionKeyClient.PublishAsync(
            identity.PublicKeyBase64, identity.WrapWithPassword(newPassword), cancellationToken);

        return EncryptionKeyOutcome.Unlocked;
    }

    /// <summary>
    /// Replaces the key outright, publishing a fresh one under <paramref name="newPassword"/>.
    ///
    /// The one path allowed to discard a key the account still has, because it is the one the user
    /// deliberately chose: after a password reset, nobody - including them - can ever open the old
    /// backup again, so refusing to replace it would leave chat permanently locked instead of merely
    /// starting over. Every other path refuses on purpose (see the class remarks); this is why that
    /// refusal is "not without being asked" rather than "never".
    ///
    /// The caller must have told the user that their existing messages become unreadable.
    /// </summary>
    public async Task<EncryptionKeyOutcome> ReplaceAfterPasswordResetAsync(
        string newPassword, CancellationToken cancellationToken = default)
    {
        var userId = await RequireSignedInUserIdAsync();
        return await CreateAndPublishAsync(userId, newPassword, cancellationToken);
    }

    private async Task<EncryptionKeyOutcome> RestoreOrCreateAsync(
        Guid userId, string password, CancellationToken cancellationToken)
    {
        var lookup = await _encryptionKeyClient.FindBackupAsync(cancellationToken);

        switch (lookup.Outcome)
        {
            case BackupLookupOutcome.CouldNotAsk:
                _logger.LogWarning(
                    "Could not ask whether a chat key backup exists; leaving the key locked rather than replacing it");
                return EncryptionKeyOutcome.StillLocked;

            case BackupLookupOutcome.ServerHasNone:
                return await CreateAndPublishAsync(userId, password, cancellationToken);

            default:
                return await RestoreAsync(userId, lookup.Backup!, password, cancellationToken);
        }
    }

    private async Task<EncryptionKeyOutcome> RestoreAsync(
        Guid userId, Orbit.Contracts.Users.WrappedPrivateKeyDto backup, string password, CancellationToken cancellationToken)
    {
        using var restored = ChatIdentity.FromBackup(backup, password);
        if (restored is null)
        {
            // Wrapped under a different password than the one that just signed the user in. The key it
            // holds is still the real one, so it is not ours to throw away.
            _logger.LogWarning("A chat key backup exists but this password does not open it; leaving the key locked");
            return EncryptionKeyOutcome.StillLocked;
        }

        await _keyStorage.WritePrivateKeyJwkAsync(userId, restored.ExportPrivateKeyJwk());
        await TryPublishPublicKeyAsync(restored, cancellationToken);
        return EncryptionKeyOutcome.Unlocked;
    }

    private async Task<EncryptionKeyOutcome> CreateAndPublishAsync(
        Guid userId, string password, CancellationToken cancellationToken)
    {
        using var created = ChatIdentity.Create();
        await _keyStorage.WritePrivateKeyJwkAsync(userId, created.ExportPrivateKeyJwk());
        await TryPublishBackupAsync(created, password, cancellationToken);
        return EncryptionKeyOutcome.Created;
    }

    /// <summary>
    /// Best-effort, like the web client's: the key already works locally, and a failure to publish is
    /// retried on the next sign-in rather than being made the user's problem now.
    /// </summary>
    private async Task TryPublishBackupAsync(ChatIdentity identity, string password, CancellationToken cancellationToken)
    {
        try
        {
            await _encryptionKeyClient.PublishAsync(
                identity.PublicKeyBase64, identity.WrapWithPassword(password), cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Could not publish the chat key backup ({Reason}); it will be retried", exception.Message);
        }
    }

    private async Task TryPublishPublicKeyAsync(ChatIdentity identity, CancellationToken cancellationToken)
    {
        try
        {
            await _encryptionKeyClient.PublishPublicKeyAsync(identity.PublicKeyBase64, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Could not publish the chat public key ({Reason}); it will be retried", exception.Message);
        }
    }

    private async Task<Guid> RequireSignedInUserIdAsync()
        => await _sessionStore.GetAsync() is { } session
            ? session.UserId
            : throw new EncryptionKeyLockedException();
}
