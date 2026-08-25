using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.GetChatGroups;

public sealed class GetChatGroupsQueryHandler : IRequestHandler<GetChatGroupsQuery, IReadOnlyList<ChatGroup>>
{
    private readonly IChatGroupRepository _chatGroupRepository;

    public GetChatGroupsQueryHandler(IChatGroupRepository chatGroupRepository)
    {
        _chatGroupRepository = chatGroupRepository;
    }

    public Task<IReadOnlyList<ChatGroup>> HandleAsync(GetChatGroupsQuery request, CancellationToken cancellationToken)
        => _chatGroupRepository.GetForMemberAsync(request.UserId, cancellationToken);
}
