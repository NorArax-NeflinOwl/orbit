namespace Orbit.Contracts.Users;

/// <param name="CiphertextBase64">The AES-GCM-encrypted, JSON-serialized private key (JWK format).</param>
/// <param name="NonceBase64">The AES-GCM nonce used to produce <see cref="CiphertextBase64"/>.</param>
/// <param name="SaltBase64">The PBKDF2 salt used to derive the wrapping key from the account password.</param>
/// <param name="Iterations">The PBKDF2 iteration count used alongside <see cref="SaltBase64"/>.</param>
public sealed record WrappedPrivateKeyDto(string CiphertextBase64, string NonceBase64, string SaltBase64, int Iterations);
