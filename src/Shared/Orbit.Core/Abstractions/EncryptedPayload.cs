namespace Orbit.Core.Abstractions;

/// <summary>
/// Content Orbit's server holds but cannot read: AES-GCM ciphertext and the nonce it was sealed with,
/// both base64. Sealed in the browser under a key derived from the owner's own encryption key pair (see
/// e2eeChat.js's encryptForSelf), which never leaves that browser in the clear, so the server has no way
/// to open it - the same guarantee chat messages already have, applied to something with one reader
/// instead of two.
///
/// Anything carrying one of these keeps no readable copy of what it encrypts: a private note's title and
/// lines are empty server-side, not merely hidden. That is what makes the promise checkable rather than
/// a matter of trust - and it is also why a server-side feature that needs to read content (a reminder
/// about a due date, say) cannot work on private items.
/// </summary>
public sealed record EncryptedPayload(string Ciphertext, string Nonce);
