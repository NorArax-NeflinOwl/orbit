using Orbit.Core.Abstractions;
using Orbit.Core.Chat.Groups.SendGroupMessage;

namespace Orbit.Core.Chat.Groups.EditGroupMessage;

/// <summary>
/// A group message edited to new text, already encrypted once per recipient by the sender's browser -
/// the same fan-out sending needs, for the same reason: the server holds ciphertext it cannot open, so
/// it can neither re-encrypt nor even tell that the copies say the same thing.
///
/// Addressed by GroupMessageId rather than by one copy's id, because "the message" is every copy of it.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record EditGroupMessageCommand(
    Guid RequestingUserId, Guid GroupMessageId, IReadOnlyList<GroupMessageCopy> Copies) : IRequest<bool>;
