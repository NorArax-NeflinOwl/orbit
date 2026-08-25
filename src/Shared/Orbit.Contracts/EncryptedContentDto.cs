namespace Orbit.Contracts;

/// <summary>
/// The sealed title and content of a private note or task list, as it travels over the wire: base64
/// AES-GCM ciphertext and the base64 nonce it was sealed with. Orbit.Api stores both and can read
/// neither - see Orbit.Core.Abstractions.EncryptedPayload.
/// </summary>
public sealed record EncryptedContentDto(string Ciphertext, string Nonce);
