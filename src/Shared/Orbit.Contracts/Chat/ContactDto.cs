namespace Orbit.Contracts.Chat;

/// <param name="RequiresApprovalFromCurrentUser">
/// True when this is a chat request someone else sent that the current user hasn't approved yet - see
/// ChatConversationAccess. Drives the "new request" badge on Contacts.razor's chat list.
/// </param>
/// <param name="IsPendingApprovalFromOtherParty">
/// True when the current user sent this chat request and the other party hasn't approved it yet - see
/// ChatConversationAccess. Lets Dashboard.razor's "Contacts" column hide a conversation that isn't
/// established yet, even though there's nothing for the current user to approve.
/// </param>
public sealed record ContactDto(
    Guid UserId, string UserName, string DisplayName, string Email, string? PublicKeyBase64, DateTimeOffset LastMessageAtUtc,
    bool RequiresApprovalFromCurrentUser, bool IsPendingApprovalFromOtherParty);
