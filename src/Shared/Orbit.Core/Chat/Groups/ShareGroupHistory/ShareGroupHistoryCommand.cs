using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.ShareGroupHistory;

/// <summary>
/// One already-posted message, re-encrypted by the sharer for the newcomer. Only the ciphertext travels:
/// who wrote the message and when are read from the copy the server already holds, so a share cannot
/// restate them - see ChatMessage.CreateSharedHistoryCopy.
/// </summary>
public sealed record SharedHistoryCopy(Guid GroupMessageId, string CiphertextBase64, string NonceBase64);

/// <summary>
/// Hands a group's past to somebody who joined after it happened. The server cannot do this itself - it
/// has never held a key to any of it - so the work is the sharer's browser's: decrypt what it can
/// already read, seal each message again under the pairwise key it shares with the newcomer, and send
/// the results here.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record ShareGroupHistoryCommand(
    Guid ActorUserId, Guid GroupId, Guid RecipientUserId, IReadOnlyList<SharedHistoryCopy> Copies) : IRequest<int>;
