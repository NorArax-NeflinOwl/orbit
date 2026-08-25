using System.Security.Cryptography;
using System.Text;
using Orbit.Core.Abstractions;

namespace Orbit.Api.Auth;

/// <summary>
/// Six-digit codes for email verification and password resets. Kept as plain infrastructure alongside
/// <see cref="PasswordHasher"/> rather than an Orbit.Core service, for the same reason.
///
/// Hashed with a single SHA-256 pass, unlike passwords: a code is high-rate-checked, lives for minutes,
/// dies after five wrong guesses, and is drawn from a 10^6 space that no key-stretching could meaningfully
/// protect anyway - the lifetime and attempt cap are what secure it (see UserVerificationCode). Deliberately
/// unsalted so a code can be looked up by hash comparison without a per-row round trip.
/// </summary>
public sealed class VerificationCodeGenerator : IVerificationCodeGenerator
{
    private const int Digits = 6;

    public string Generate() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString($"D{Digits}");

    public string Hash(string code) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim())));

    /// <summary>Fixed-time comparison, so a wrong code can't be narrowed down by timing how long the check took.</summary>
    public bool Verify(string code, string codeHash)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(code)), Encoding.UTF8.GetBytes(codeHash));
}
