using System.ComponentModel.DataAnnotations.Schema;

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

    /// <summary>
    /// How to reach them outside Orbit, for the card that says who somebody is. Cached with the rest of
    /// the row rather than looked up when the card opens: that card has to answer offline, and an
    /// address is the one line on it nothing else stands in for.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Whether that address is one Google itself has verified, which it is for an account signed in with
    /// Google - see ContactDto.HasGoogleVerifiedEmail. Cached with the row for the same reason the
    /// address is: what it guards is a link built on this phone, offline as often as not.
    /// </summary>
    public bool HasGoogleVerifiedEmail { get; set; }

    public string? PublicKeyBase64 { get; set; }

    public DateTimeOffset LastMessageAtUtc { get; set; }

    /// <summary>
    /// When this person was last here - see ContactDto.LastSeenAtUtc, which has been arriving on the
    /// phone all along and was simply not kept. Stored because the card that reads it has to answer
    /// offline like everything else on the dashboard, and because "when was there last anything here"
    /// is the later of this and the message above (Orbit.Core.Chat.ConversationRecency).
    ///
    /// Null for an account nobody has ever seen, and for every row stored before this was kept - in
    /// which case the message is the whole answer, which is exactly what the card said before.
    /// </summary>
    public DateTimeOffset? LastSeenAtUtc { get; set; }

    /// <summary>
    /// Put away by this reader, and by nobody else - see ContactDto.IsArchived. One-sided on purpose:
    /// the other party's list has its own row and its own answer.
    /// </summary>
    public bool IsArchived { get; set; }

    /// <summary>
    /// Kept at the top of this reader's list, on this device only - see ConversationPins. Not stored
    /// with the row: pinning is one person's answer about one screen, and the server has no business
    /// knowing it.
    /// </summary>
    [NotMapped]
    public bool IsPinned { get; set; }

    /// <summary>
    /// When the last message this phone holds from this conversation was sent, read off the messages
    /// rather than off the row - see ChatRepository.LastMessageTimesAsync, which says why the row's own
    /// answer goes stale. Null where this phone holds none of their messages, and then the row's answer
    /// is the only one there is.
    ///
    /// Not stored: it is a fact about the messages beside it, and writing it down would give it a second
    /// chance to disagree with them.
    /// </summary>
    [NotMapped]
    public DateTimeOffset? LastMessageHeldAtUtc { get; set; }

    /// <summary>
    /// When there was last anything to read here - the message this phone actually holds where there is
    /// one, and what the row says otherwise.
    /// </summary>
    public DateTimeOffset LastMessageShown => LastMessageHeldAtUtc ?? LastMessageAtUtc;

    /// <summary>
    /// That moment in the reader's own words, for the row that draws it - see RelativeMoment. Put here
    /// by the screen rather than worked out here: this is a stored row and has no dictionary, and the
    /// row it is drawn on has none either.
    /// </summary>
    [NotMapped]
    public string WhenShown { get; set; } = string.Empty;

    /// <summary>
    /// How many of their messages this reader has not read - ContactDto.UnreadCount, which the server
    /// works out from the read mark it keeps per conversation. Kept with the row, like everything else on
    /// it, so the count survives a restart and the list says it offline; the next refresh brings it up to
    /// date, and opening the conversation takes it to nought on the spot (ChatRepository.MarkReadAsync)
    /// rather than waiting for that refresh.
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// Something here is waiting on this reader - a request to answer, or messages to read. What the
    /// row's mark says; Orbit.Web marks a row for the same two reasons.
    /// </summary>
    [NotMapped]
    public bool HasSomethingWaiting => RequiresApprovalFromCurrentUser || UnreadCount > 0;

    /// <summary>A chat request this user sent that the signed-in user hasn't approved yet.</summary>
    public bool RequiresApprovalFromCurrentUser { get; set; }

    /// <summary>A chat request the signed-in user sent that this person hasn't approved yet.</summary>
    public bool IsPendingApprovalFromOtherParty { get; set; }

    /// <summary>
    /// What to show beside this person's name: "Available", "Away", "DoNotDisturb" or "Offline" - see
    /// Orbit.Core.Users.PresenceStatus. The server resolves it when the list is read, so it ages the
    /// moment it arrives; the chat list refreshes on the same poll that keeps the rest current.
    ///
    /// Cached with the rest of the row rather than left out because the list has to open offline, and a
    /// row with a missing dot reads as a different person from one with a grey dot.
    /// </summary>
    public string PresenceStatus { get; set; } = nameof(Orbit.Core.Users.PresenceStatus.Offline);
}
