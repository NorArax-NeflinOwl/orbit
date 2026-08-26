using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.GetGroupMessageReceipts;

public sealed class GetGroupMessageReceiptsQueryHandler
    : IRequestHandler<GetGroupMessageReceiptsQuery, IReadOnlyList<GroupMessageReceipt>>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IChatMessageRepository _chatMessageRepository;

    public GetGroupMessageReceiptsQueryHandler(
        IChatGroupRepository chatGroupRepository, IChatMessageRepository chatMessageRepository)
    {
        _chatGroupRepository = chatGroupRepository;
        _chatMessageRepository = chatMessageRepository;
    }

    /// <summary>
    /// Empty for a group the caller isn't in, the same answer a group that doesn't exist gives - who a
    /// message reached is only the business of the people it reached.
    ///
    /// Carries ids rather than names: the caller is looking at the group, so it already holds the roster
    /// - and a name is the sort of thing that changes after the fact, which a receipt should not.
    /// </summary>
    public async Task<IReadOnlyList<GroupMessageReceipt>> HandleAsync(
        GetGroupMessageReceiptsQuery request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.UserId))
        {
            return [];
        }

        var receipts = await _chatMessageRepository.GetGroupReceiptsAsync([request.GroupMessageId], cancellationToken);
        return receipts.GetValueOrDefault(request.GroupMessageId, []);
    }
}
