namespace Orbit.Core.Chat;

/// <summary>
/// A contact list entry: the other party's current public profile joined with when this conversation
/// was last active. Kept separate from <see cref="Contact"/> itself, which only stores the ids - the
/// profile fields are always read live (see GetContactsQueryHandler) rather than cached on the row.
/// </summary>
/// <param name="RequiresApprovalFromCurrentUser">
/// True when the other party started this conversation and the current user hasn't approved chatting
/// with them yet - see ChatConversationAccess. Lets the contact list show it as a pending request
/// instead of an established chat.
/// </param>
/// <param name="IsPendingApprovalFromOtherParty">
/// True when the current user started this conversation and the other party hasn't approved chatting
/// yet - see ChatConversationAccess. Lets a contact list distinguish this from an established chat even
/// though, unlike <see cref="RequiresApprovalFromCurrentUser"/>, there's nothing for the current user to
/// act on yet.
/// </param>
public sealed record ContactSummary(
    Orbit.Core.Users.User User, DateTimeOffset LastMessageAtUtc, bool RequiresApprovalFromCurrentUser,
    bool IsPendingApprovalFromOtherParty, int UnreadCount,
    /// <summary>Put away on this reader's own list - see Contact.IsArchived.</summary>
    bool IsArchived = false);
