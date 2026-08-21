namespace Orbit.Contracts.Users;

/// <summary>
/// Publishes the caller's public key together with a password-encrypted backup of the matching private
/// key - see WrappedPrivateKeyDto. Sent as one call because the two must always agree: a wrapped private
/// key that doesn't match the published public key would be useless.
/// </summary>
public sealed record SetEncryptionKeyRequest(string PublicKeyBase64, WrappedPrivateKeyDto WrappedPrivateKey);
