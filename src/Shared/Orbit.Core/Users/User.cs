namespace Orbit.Core.Users;

/// <summary>
/// An Orbit account. Owns notes and, eventually, every other per-user resource in the app.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string UserName { get; private set; }
    public string DisplayName { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// The browser-generated ECDH public key (raw bytes, base64) used for end-to-end-encrypted chat -
    /// see wwwroot/js/e2eeChat.js. Null until the user has opened the chat feature at least once; the
    /// matching private key never leaves the browser that generated it, except as the encrypted backup
    /// in <see cref="WrappedPrivateKey"/>.
    /// </summary>
    public string? PublicKeyBase64 { get; private set; }

    /// <summary>
    /// A password-encrypted backup of the private key matching <see cref="PublicKeyBase64"/> - see
    /// WrappedPrivateKey. Null for a user who hasn't logged in since this backup was introduced, or
    /// whose browser holds a private key generated before then and never re-wrapped (see
    /// OwnEncryptionKeyProvider.UnlockOrCreateAsync) - in both cases the only local copy still lives
    /// solely in whichever browser generated it, exactly as before this existed.
    /// </summary>
    public WrappedPrivateKey? WrappedPrivateKey { get; private set; }

    private User(
        Guid id, string email, string userName, string displayName, string passwordHash, DateTimeOffset createdAtUtc,
        string? publicKeyBase64, WrappedPrivateKey? wrappedPrivateKey)
    {
        Id = id;
        Email = email;
        UserName = userName;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
        PublicKeyBase64 = publicKeyBase64;
        WrappedPrivateKey = wrappedPrivateKey;
    }

    public static User Create(string email, string userName, string displayName, string passwordHash)
        => new(
            Guid.NewGuid(), email, userName, displayName, passwordHash, DateTimeOffset.UtcNow, publicKeyBase64: null,
            wrappedPrivateKey: null);

    /// <summary>
    /// Rebuilds a user from already-persisted values, bypassing creation rules.
    /// </summary>
    public static User FromPersistence(
        Guid id, string email, string userName, string displayName, string passwordHash, DateTimeOffset createdAtUtc,
        string? publicKeyBase64, WrappedPrivateKey? wrappedPrivateKey = null)
        => new(id, email, userName, displayName, passwordHash, createdAtUtc, publicKeyBase64, wrappedPrivateKey);

    /// <summary>
    /// Replaces the stored public key with the one the browser currently reports. Overwrites any
    /// previous key outright - only the newest one is usable, since the matching private key for an
    /// older one may no longer exist anywhere.
    /// </summary>
    public void SetPublicKey(string publicKeyBase64)
    {
        PublicKeyBase64 = publicKeyBase64;
    }

    /// <summary>
    /// Replaces both the public key and its password-encrypted private key backup together - the two
    /// always change as a pair (see OwnEncryptionKeyProvider.UnlockOrCreateAsync), since a wrapped
    /// private key that doesn't match the currently published public key would be useless.
    /// </summary>
    public void SetEncryptionKey(string publicKeyBase64, WrappedPrivateKey wrappedPrivateKey)
    {
        PublicKeyBase64 = publicKeyBase64;
        WrappedPrivateKey = wrappedPrivateKey;
    }
}
