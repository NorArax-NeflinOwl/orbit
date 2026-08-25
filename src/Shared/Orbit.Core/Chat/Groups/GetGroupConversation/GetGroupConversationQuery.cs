using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.GetGroupConversation;

public sealed record GetGroupConversationQuery(Guid UserId, Guid GroupId) : IRequest<IReadOnlyList<ChatMessage>>;
