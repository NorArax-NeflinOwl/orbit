namespace Orbit.Data.Entities;

/// <summary>Persistence shape of <see cref="Orbit.Core.Users.UserVerificationCode"/> - the code itself is stored hashed, never in plaintext.</summary>
public sealed class UserVerificationCodeEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Users.VerificationCodePurpose"/> - "EmailVerification"/"PasswordReset".</summary>
    public string Purpose { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public int FailedAttempts { get; set; }
}
