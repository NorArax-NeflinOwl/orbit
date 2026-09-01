using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.SetConversationArchived;

/// <summary>
/// Puts one conversation away on the caller's own list, or brings it back.
///
/// Archiving is not deleting and not leaving: every message stays where it is, the other party is told
/// nothing, and their own list is untouched. It is a fact about how one person reads their own screen -
/// which is why the command carries the caller rather than a row id, and why there is no notion of
/// archiving a conversation "for everybody".
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record SetConversationArchivedCommand(Guid UserId, Guid OtherUserId, bool IsArchived)
    : IRequest<bool>;
