using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.GetGroupConversation;

/// <summary>
/// A group's messages, optionally only those after <paramref name="SinceUtc"/> - the cursor a client
/// polling this needs so it stops asking for the whole conversation on every tick. Null means all of it,
/// which is what a window opening for the first time wants.
/// </summary>
public sealed record GetGroupConversationQuery(Guid UserId, Guid GroupId, DateTimeOffset? SinceUtc = null)
    : IRequest<IReadOnlyList<ChatMessage>>;
