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
    /// <summary>
    /// Null for an account created through Google that hasn't set a password yet - such an account can
    /// sign in, but can't use chat until it has one, because the chat key backup is wrapped with it (see
    /// WrappedPrivateKey). Setting one turns the account into an ordinary email+password account that
    /// also happens to be linked to Google.
    /// </summary>
    public string? PasswordHash { get; private set; }

    public bool HasPassword => PasswordHash is not null;

    /// <summary>
    /// Google's stable subject id for this account, once linked. Matched on instead of the email address
    /// because a Google account's address can change, while the subject never does.
    /// </summary>
    public string? GoogleSubjectId { get; private set; }

    /// <summary>
    /// Where this user last recorded themselves as being, or null if they never have or have since
    /// cleared it. Only ever set by the user themselves - see SaveOwnLocationCommandHandler.
    /// </summary>
    public UserLocation? Location { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// When the user last proved they can read mail at <see cref="Email"/>, or null if they never have.
    /// Only a verified address may receive a password reset, since a reset email sent to an address the
    /// user doesn't control would hand over the account.
    /// </summary>
    public DateTimeOffset? EmailVerifiedAtUtc { get; private set; }

    public bool IsEmailVerified => EmailVerifiedAtUtc is not null;

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

    /// <summary>What this account chose to be, and when it was last heard from - see <see cref="UserPresence"/>.</summary>
    public UserPresence Presence { get; private set; } = UserPresence.NeverSeen;

    /// <summary>
    /// Whether this account has asked that nothing about it reach anybody but Orbit - the footer's
    /// "Do not share my personal information".
    ///
    /// It is about the third parties a page brings with it rather than about sharing a note with a
    /// contact, which is the account's own doing and is asked for a note at a time. What it turns off
    /// is named on the Privacy page: the map's tiles, and the trace this deployment keeps of what its
    /// server was doing. The fonts and the map's own code were moved into Orbit's own wwwroot rather
    /// than gated, because a page that looks different depending on a privacy choice is a page that
    /// tells everybody what the choice was.
    ///
    /// Kept on the account rather than in the browser so it follows a reader between devices - it is a
    /// standing instruction, not a preference about this screen. The browser mirrors it for the first
    /// paint; see BrowserStorageConsent's necessary keys.
    /// </summary>
    public bool KeepsThirdPartiesOut { get; private set; }

    private User(
        Guid id, string email, string userName, string displayName, string? passwordHash, DateTimeOffset createdAtUtc,
        string? publicKeyBase64, WrappedPrivateKey? wrappedPrivateKey, DateTimeOffset? emailVerifiedAtUtc, string? googleSubjectId)
    {
        EmailVerifiedAtUtc = emailVerifiedAtUtc;
        GoogleSubjectId = googleSubjectId;
        Id = id;
        Email = email;
        UserName = userName;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
        PublicKeyBase64 = publicKeyBase64;
        WrappedPrivateKey = wrappedPrivateKey;
    }

    /// <summary>A freshly registered account's email is unverified until they confirm a code sent to it.</summary>
    public static User Create(string email, string userName, string displayName, string passwordHash)
        => new(
            Guid.NewGuid(), email, userName, displayName, passwordHash, DateTimeOffset.UtcNow, publicKeyBase64: null,
            wrappedPrivateKey: null, emailVerifiedAtUtc: null, googleSubjectId: null);

    /// <summary>
    /// An account created by signing in with Google. It has no password until the user sets one, and its
    /// address arrives already verified - Google only issues an email_verified token for an address it
    /// has itself confirmed, which is exactly the property a password reset depends on.
    /// </summary>
    public static User CreateFromGoogle(string email, string userName, string displayName, string googleSubjectId)
        => new(
            Guid.NewGuid(), email, userName, displayName, passwordHash: null, DateTimeOffset.UtcNow, publicKeyBase64: null,
            wrappedPrivateKey: null, emailVerifiedAtUtc: DateTimeOffset.UtcNow, googleSubjectId);

    /// <summary>
    /// Rebuilds a user from already-persisted values, bypassing creation rules.
    /// </summary>
    public static User FromPersistence(
        Guid id, string email, string userName, string displayName, string? passwordHash, DateTimeOffset createdAtUtc,
        string? publicKeyBase64, WrappedPrivateKey? wrappedPrivateKey = null, DateTimeOffset? emailVerifiedAtUtc = null,
        string? googleSubjectId = null, UserLocation? location = null, UserPresence? presence = null,
        bool keepsThirdPartiesOut = false)
    {
        var user = new User(
            id, email, userName, displayName, passwordHash, createdAtUtc, publicKeyBase64, wrappedPrivateKey,
            emailVerifiedAtUtc, googleSubjectId);
        user.Location = location;
        user.Presence = presence ?? UserPresence.NeverSeen;
        user.KeepsThirdPartiesOut = keepsThirdPartiesOut;
        return user;
    }

    /// <summary>Answers the footer's "Do not share my personal information" - see <see cref="KeepsThirdPartiesOut"/>.</summary>
    public void SetKeepsThirdPartiesOut(bool keepsThemOut) => KeepsThirdPartiesOut = keepsThemOut;

    /// <summary>Records that this account is here right now - see PresenceHeartbeatCommandHandler.</summary>
    public void RecordSeen(DateTimeOffset nowUtc) => Presence = Presence.SeenAt(nowUtc);

    /// <summary>
    /// Changes what this person chose to be. Counts as being seen too: choosing a status is itself proof
    /// that somebody is at the keyboard, and without it setting "available" from a stale session would
    /// leave them showing as offline until the next heartbeat.
    /// </summary>
    public void SetAvailability(PresenceAvailability availability, DateTimeOffset nowUtc)
        => Presence = Presence.WithAvailability(availability).SeenAt(nowUtc);

    /// <summary>Ties an existing account to a Google identity, so signing in with Google finds this account instead of creating a second one.</summary>
    public void LinkGoogle(string googleSubjectId) => GoogleSubjectId = googleSubjectId;

    public void UnlinkGoogle() => GoogleSubjectId = null;

    public void ChangeDisplayName(string displayName) => DisplayName = displayName;

    /// <summary>
    /// Replaces whatever was recorded before - there is only ever one point per user, so recording a new
    /// one is how the old one goes away. Passing null clears it outright.
    /// </summary>
    public void RecordLocation(UserLocation? location) => Location = location;

    /// <summary>Callers are expected to have already rejected a login that is taken by someone else - see ChangeUserNameCommandHandler.</summary>
    public void ChangeUserName(string userName) => UserName = userName;

    /// <summary>
    /// Points the account at an address whose ownership was just proved by a confirmed code, and marks it
    /// verified. This is the only way <see cref="Email"/> ever changes - see
    /// VerificationCodePurpose.EmailVerification for why an unproven address is never written.
    /// </summary>
    public void SetVerifiedEmail(string email)
    {
        Email = email;
        EmailVerifiedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Callers are expected to have already established the right to do this - either by checking the
    /// current password, or by confirming a password-reset code. Note that this does *not* re-wrap the
    /// chat key backup, which only the browser can do (see OwnEncryptionKeyProvider.RewrapAsync): the
    /// client re-wraps immediately afterwards, in the same flow.
    /// </summary>
    public void ChangePassword(string passwordHash) => PasswordHash = passwordHash;

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
