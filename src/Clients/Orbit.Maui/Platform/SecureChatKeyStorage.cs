using Orbit.Mobile.Crypto;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps the chat private key in the platform's secure store - Keychain on iOS, Keystore-backed storage
/// on Android - one entry per user id, so two accounts on one phone never share a key.
///
/// The key is stored as exportable JWK rather than being generated inside the Secure Enclave. That looks
/// like the weaker choice and is the required one: Orbit's password change re-wraps the private key
/// under the new password, which means exporting it. A hardware-backed, non-exportable key would make
/// changing a password silently destroy the user's chat history - see
/// <see cref="IChatKeyStorage"/> and info/orbit-maui-plan.md §4.1. Access is gated at the app level
/// instead.
/// </summary>
public sealed class SecureChatKeyStorage : IChatKeyStorage
{
	private readonly ISecureStorage _secureStorage;

	public SecureChatKeyStorage(ISecureStorage secureStorage) => _secureStorage = secureStorage;

	public async Task<string?> ReadPrivateKeyJwkAsync(Guid userId)
	{
		var stored = await _secureStorage.GetAsync(StorageKeyFor(userId));
		return string.IsNullOrEmpty(stored) ? null : stored;
	}

	public Task WritePrivateKeyJwkAsync(Guid userId, string privateKeyJwk)
		=> _secureStorage.SetAsync(StorageKeyFor(userId), privateKeyJwk);

	private static string StorageKeyFor(Guid userId) => $"orbit.chat-key:{userId}";
}
