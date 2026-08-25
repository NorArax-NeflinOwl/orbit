namespace Orbit.Core.Users;

/// <summary>
/// A short numeric code emailed to a user to prove they can read mail at an address - used both for
/// verifying an address and for resetting a forgotten password. Stored hashed and single-use, mirroring
/// <see cref="RefreshToken"/>, so a leaked database row can't be replayed as a working code.
///
/// A six-digit code is deliberately low-entropy (it has to be retyped by hand), so three things carry
/// the security here rather than the code itself: a short lifetime, a hard cap on wrong guesses
/// (<see cref="MaxFailedAttempts"/>) after which the code dies, and the rate limiter on the endpoints
/// that issue and redeem it.
/// </summary>
public sealed class UserVerificationCode
{
    /// <summary>Long enough to fetch an email and retype a code, short enough to bound guessing.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    public const int MaxFailedAttempts = 5;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public VerificationCodePurpose Purpose { get; private set; }
    public string CodeHash { get; private set; }

    /// <summary>
    /// The address the code was sent to. For <see cref="VerificationCodePurpose.EmailVerification"/>
    /// this is also the address the account switches to on success - see that member's comment.
    /// </summary>
    public string EmailAddress { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public int FailedAttempts { get; private set; }

    public bool IsActive
        => ConsumedAtUtc is null && FailedAttempts < MaxFailedAttempts && ExpiresAtUtc > DateTimeOffset.UtcNow;

    private UserVerificationCode(
        Guid id, Guid userId, VerificationCodePurpose purpose, string codeHash, string emailAddress,
        DateTimeOffset expiresAtUtc, DateTimeOffset createdAtUtc, DateTimeOffset? consumedAtUtc, int failedAttempts)
    {
        Id = id;
        UserId = userId;
        Purpose = purpose;
        CodeHash = codeHash;
        EmailAddress = emailAddress;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = createdAtUtc;
        ConsumedAtUtc = consumedAtUtc;
        FailedAttempts = failedAttempts;
    }

    public static UserVerificationCode Create(Guid userId, VerificationCodePurpose purpose, string codeHash, string emailAddress)
    {
        var now = DateTimeOffset.UtcNow;
        return new UserVerificationCode(
            Guid.NewGuid(), userId, purpose, codeHash, emailAddress, now.Add(Lifetime), now, consumedAtUtc: null, failedAttempts: 0);
    }

    /// <summary>Rebuilds a code from already-persisted values, bypassing creation rules.</summary>
    public static UserVerificationCode FromPersistence(
        Guid id, Guid userId, VerificationCodePurpose purpose, string codeHash, string emailAddress,
        DateTimeOffset expiresAtUtc, DateTimeOffset createdAtUtc, DateTimeOffset? consumedAtUtc, int failedAttempts)
        => new(id, userId, purpose, codeHash, emailAddress, expiresAtUtc, createdAtUtc, consumedAtUtc, failedAttempts);

    public void Consume() => ConsumedAtUtc ??= DateTimeOffset.UtcNow;

    /// <summary>Counts a wrong guess; once <see cref="MaxFailedAttempts"/> is reached the code stops being usable at all.</summary>
    public void RecordFailedAttempt() => FailedAttempts++;
}
