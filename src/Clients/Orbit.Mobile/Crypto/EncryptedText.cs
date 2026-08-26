namespace Orbit.Mobile.Crypto;

/// <summary>
/// Something sealed with AES-GCM, in the shape it travels in: base64 ciphertext and the base64 nonce it
/// was sealed with. The nonce is not a secret and must travel with the ciphertext - decryption needs
/// exactly the same value.
///
/// The ciphertext has the 16-byte GCM tag appended, because that is what WebCrypto produces and Orbit's
/// wire format is whatever the browser already sends. .NET keeps the tag in its own buffer, so
/// <see cref="ChatIdentity"/> splits it off on the way in and appends it on the way out.
/// </summary>
public sealed record EncryptedText(string CiphertextBase64, string NonceBase64);
