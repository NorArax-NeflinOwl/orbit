using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.ClearConversationHistory;

/// <summary>
/// Empties one conversation as the caller sees it. Everything sent up to now stops being shown to
/// them, and the other party keeps every word of it.
///
/// It is not a delete for both sides, and deliberately so: a one-to-one message is one row that both
/// people read, so deleting it would take words out of somebody else's conversation - which is not a
/// thing one party gets to decide. See Contact.HistoryClearedAtUtc.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record ClearConversationHistoryCommand(Guid UserId, Guid OtherUserId) : IRequest<bool>;
