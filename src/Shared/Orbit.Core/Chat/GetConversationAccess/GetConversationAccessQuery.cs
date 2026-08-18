using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.GetConversationAccess;

public sealed record GetConversationAccessQuery(Guid UserId, Guid OtherUserId) : IRequest<ChatConversationAccess?>;
