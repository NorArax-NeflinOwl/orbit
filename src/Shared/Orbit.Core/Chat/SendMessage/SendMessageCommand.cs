using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.SendMessage;

[ClientAction(ClientActionCategory.SendMessage)]
/// <param name="IsShareInvitation">
/// Marks the structured message that carries a share's "Accept" action, sent by the editors right after
/// the share itself. The share has already told the recipient about it (see SharedItemNotifier), so this
/// message doesn't announce itself a second time - the alternative is two entries in the feed for one
/// invitation, one of them the useless "New message". The server can't tell on its own: the content is
/// encrypted, so only the sender knows what it is.
/// </param>
public sealed record SendMessageCommand(
    Guid SenderUserId, Guid RecipientUserId, string CiphertextBase64, string NonceBase64, bool IsShareInvitation = false)
    : IRequest<SendMessageResult>;
