using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.GetChatGroups;

public sealed record GetChatGroupsQuery(Guid UserId) : IRequest<IReadOnlyList<ChatGroup>>;
