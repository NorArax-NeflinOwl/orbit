namespace Orbit.Contracts.Chat;

/// <param name="RequiresApprovalFromCurrentUser">
/// True when this is a chat request someone else sent that the current user hasn't approved yet - see
/// ChatConversationAccess. Drives the "nowa prośba" badge on Contacts.razor's chat list.
/// </param>
public sealed record ContactDto(
    Guid UserId, string UserName, string DisplayName, string Email, string? PublicKeyBase64, DateTimeOffset LastMessageAtUtc,
    bool RequiresApprovalFromCurrentUser);
