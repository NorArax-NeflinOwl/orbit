using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.GetGroupMessageReceipts;

/// <summary>Who one group message reached, and which of them have read it. Answerable to any member of the group.</summary>
public sealed record GetGroupMessageReceiptsQuery(Guid UserId, Guid GroupId, Guid GroupMessageId)
    : IRequest<IReadOnlyList<GroupMessageReceipt>>;
