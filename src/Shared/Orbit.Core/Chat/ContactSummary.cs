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
public sealed record ContactSummary(Orbit.Core.Users.User User, DateTimeOffset LastMessageAtUtc, bool RequiresApprovalFromCurrentUser);
