namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a user account, mapped separately from <see cref="Orbit.Core.Users.User"/> so
/// schema changes don't force changes onto domain logic, and vice versa.
/// </summary>
public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Null for a Google account that hasn't set a password - see Orbit.Core.Users.User.PasswordHash.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Google's stable subject id, once the account is linked - see Orbit.Core.Users.User.GoogleSubjectId.</summary>
    public string? GoogleSubjectId { get; set; }
    /// <summary>
    /// The single point this user last recorded for themselves, if any - see Orbit.Core.Users.UserLocation.
    /// All four move together: either every one is set or none is.
    /// </summary>
    public double? LocationLatitude { get; set; }

    public double? LocationLongitude { get; set; }

    /// <summary>The address reverse geocoding resolved, when it managed to - a point can be recorded without one.</summary>
    public string? LocationAddress { get; set; }

    public DateTimeOffset? LocationRecordedAtUtc { get; set; }

    /// <summary>What this account chose to be, by PresenceAvailability name - see Orbit.Core.Users.UserPresence.</summary>
    public string PresenceAvailability { get; set; } = Orbit.Core.Users.PresenceAvailability.Available.ToString();

    /// <summary>When this account was last heard from; null for one that has never been seen since presence existed.</summary>
    public DateTimeOffset? PresenceLastSeenAtUtc { get; set; }

    /// <summary>
    /// Whether this account has asked that nothing about it reach anybody but Orbit - see
    /// Orbit.Core.Users.User.KeepsThirdPartiesOut. False for every account that has not said otherwise,
    /// which is what an existing row gets when the column is added.
    /// </summary>
    public bool KeepsThirdPartiesOut { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    /// <summary>When the user last confirmed a code sent to Email, or null if never - see Orbit.Core.Users.User.EmailVerifiedAtUtc.</summary>
    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }

    public string? PublicKeyBase64 { get; set; }

    // Together, a password-encrypted backup of the private key matching PublicKeyBase64 - see
    // Orbit.Core.Users.WrappedPrivateKey, which is what these four map to/from in UserRepository. Kept
    // as flat columns here (rather than an owned type) to match how the rest of this entity is mapped.
    public string? WrappedPrivateKeyBase64 { get; set; }
    public string? PrivateKeyWrapNonceBase64 { get; set; }
    public string? PrivateKeySaltBase64 { get; set; }
    public int? PrivateKeyDerivationIterations { get; set; }
}
