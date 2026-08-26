namespace Orbit.Mobile.Data;

/// <summary>
/// Someone the user has a conversation with, as the phone holds them. Cached so the chat list opens
/// with no connection - without it a conversation whose history *is* cached still could not be reached,
/// which made offline chat readable in principle and not in practice.
///
/// <see cref="PublicKeyBase64"/> is kept for display purposes only: whether this person can be written
/// to at all. It is deliberately <b>not</b> used to encrypt anything. Sending fetches the key fresh, so
/// a key the recipient has since replaced cannot be used to seal a message nobody can open - see
/// EncryptedChatMessageSender.
/// </summary>
public sealed class LocalContact
{
    /// <summary>
    /// Somebody found by searching, who this phone has no contact row for yet - there has been no
    /// conversation to make one. Deliberately not stored: the server decides who is a contact, and it
    /// does that when the first message is sent. Writing this down would put them in the list before any
    /// conversation existed, and the next refresh - which replaces the list wholesale - would drop them
    /// again.
    /// </summary>
    public static LocalContact ForSomebodyNotYetSpokenTo(Guid userId, string userName, string displayName, string? publicKeyBase64)
        => new()
        {
            UserId = userId,
            UserName = userName,
            DisplayName = displayName,
            PublicKeyBase64 = publicKeyBase64
        };

    public Guid UserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? PublicKeyBase64 { get; set; }

    public DateTimeOffset LastMessageAtUtc { get; set; }

    /// <summary>A chat request this user sent that the signed-in user hasn't approved yet.</summary>
    public bool RequiresApprovalFromCurrentUser { get; set; }

    /// <summary>A chat request the signed-in user sent that this person hasn't approved yet.</summary>
    public bool IsPendingApprovalFromOtherParty { get; set; }
}
