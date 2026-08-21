namespace Orbit.Core.Users;

/// <summary>
/// A user's ECDH private key, exported and encrypted (AES-GCM) with a key derived from their account
/// password via PBKDF2 - see wwwroot/js/e2eeChat.js. Orbit.Api only ever stores this wrapped form; the
/// plaintext private key never reaches the server.
/// </summary>
/// <param name="CiphertextBase64">The AES-GCM-encrypted, JSON-serialized private key (JWK format).</param>
/// <param name="NonceBase64">The AES-GCM nonce used to produce <see cref="CiphertextBase64"/>.</param>
/// <param name="SaltBase64">The PBKDF2 salt used to derive the wrapping key from the account password.</param>
/// <param name="Iterations">
/// The PBKDF2 iteration count used alongside <see cref="SaltBase64"/> - stored per-record rather than as
/// a fixed constant, so a future increase to the default doesn't invalidate backups wrapped under the
/// old one.
/// </param>
public sealed record WrappedPrivateKey(string CiphertextBase64, string NonceBase64, string SaltBase64, int Iterations);
