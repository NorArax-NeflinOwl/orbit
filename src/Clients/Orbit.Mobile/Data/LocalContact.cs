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
