namespace Orbit.Contracts.Chat;

/// <param name="IsShareInvitation">
/// True for the structured message that carries a share's "Accept" action rather than something the
/// sender typed - see SendMessageCommand.
/// </param>
public sealed record SendMessageRequest(
    Guid RecipientUserId, string CiphertextBase64, string NonceBase64, bool IsShareInvitation = false);
