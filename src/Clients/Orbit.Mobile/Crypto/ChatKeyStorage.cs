namespace Orbit.Mobile.Crypto;

/// <summary>
/// Where this device keeps its copy of the user's chat private key, as JWK.
///
/// Namespaced by user id, the same way e2eeChat.js namespaces its IndexedDB records: two accounts on one
/// phone must each get their own key rather than silently sharing or overwriting one another's.
///
/// The implementation must use the platform's secure store, and the key must stay <b>exportable</b>.
/// A hardware-backed, non-exportable key (Secure Enclave / StrongBox) is the reflexive answer and the
/// wrong one: changing the password requires exporting the private key to re-wrap it under the new one
/// (see <see cref="OwnEncryptionKeyProvider.RewrapAsync"/>), so a non-exportable key would make a
/// password change silently destroy the user's chat history.
/// </summary>
public interface IChatKeyStorage
{
    Task<string?> ReadPrivateKeyJwkAsync(Guid userId);

    Task WritePrivateKeyJwkAsync(Guid userId, string privateKeyJwk);
}
