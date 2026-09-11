using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.GetChatGroups;

public sealed class GetChatGroupsQueryHandler : IRequestHandler<GetChatGroupsQuery, IReadOnlyList<ChatGroupListing>>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IChatMessageRepository _chatMessageRepository;

    public GetChatGroupsQueryHandler(
        IChatGroupRepository chatGroupRepository, IChatMessageRepository chatMessageRepository)
    {
        _chatGroupRepository = chatGroupRepository;
        _chatMessageRepository = chatMessageRepository;
    }

    /// <summary>
    /// Every group the caller is in, each with how many of its messages the caller has not read. The
    /// counts come from one query rather than one per group, because the chat list asks on every poll
    /// tick - see IChatMessageRepository.GetGroupUnreadCountsAsync.
    /// </summary>
    public async Task<IReadOnlyList<ChatGroupListing>> HandleAsync(
        GetChatGroupsQuery request, CancellationToken cancellationToken)
    {
        var groups = await _chatGroupRepository.GetForMemberAsync(request.UserId, cancellationToken);
        var unreadCounts = await _chatMessageRepository.GetGroupUnreadCountsAsync(request.UserId, cancellationToken);

        return [.. groups.Select(group => new ChatGroupListing(group, unreadCounts.GetValueOrDefault(group.Id)))];
    }
}
