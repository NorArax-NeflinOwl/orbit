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
/// <param name="AnnouncesShareId">
/// Which share this message is the invitation to, or null for anything else. The server cannot work
/// this out for itself - the message is sealed, and the share id lives inside the sealed payload the
/// recipient decrypts - so the sender says it here, in the clear, alongside the ciphertext.
///
/// Kept apart from <paramref name="IsShareInvitation"/> rather than replacing it: a shared position is
/// an invitation with no share row behind it (it lives in OP_LOCATIONS_SHARED and is withdrawn from the
/// map), so the two are not the same question. What this one is for is the withdrawal - see
/// RevokeShareCommandHandler, which takes the announcement down with the access it announced.
/// </param>
public sealed record SendMessageCommand(
    Guid SenderUserId, Guid RecipientUserId, string CiphertextBase64, string NonceBase64, bool IsShareInvitation = false,
    Guid? AnnouncesShareId = null)
    : IRequest<SendMessageResult>;
