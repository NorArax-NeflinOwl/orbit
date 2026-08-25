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
/// <remarks>
/// Both parts are checked here rather than at each call site, so an EncryptedPayload that exists at all
/// is one with something in it. A request whose encryptedContent object is present but whose members
/// didn't bind - a client sending the wrong property names, say - arrives as a non-null payload holding
/// nulls, and without this it wrote a row marked private with nothing sealed inside it.
/// </remarks>
public sealed record EncryptedPayload
{
    public EncryptedPayload(string Ciphertext, string Nonce)
    {
        if (string.IsNullOrWhiteSpace(Ciphertext) || string.IsNullOrWhiteSpace(Nonce))
        {
            throw new InvalidRequestException("Encrypted content must carry both its ciphertext and its nonce.");
        }

        this.Ciphertext = Ciphertext;
        this.Nonce = Nonce;
    }

    public string Ciphertext { get; init; }
    public string Nonce { get; init; }
}
