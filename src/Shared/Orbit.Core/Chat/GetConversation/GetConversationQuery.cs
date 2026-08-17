using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.GetConversation;

public sealed record GetConversationQuery(Guid UserId, Guid OtherUserId, DateTimeOffset? SinceUtc) : IRequest<IReadOnlyList<ChatMessage>>;
