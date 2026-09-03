using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups.GetGroupAnnouncements;

public sealed class GetGroupAnnouncementsQueryHandler
    : IRequestHandler<GetGroupAnnouncementsQuery, IReadOnlyList<ChatGroupAnnouncement>>
{
    private readonly IChatGroupRepository _chatGroupRepository;
    private readonly IChatGroupAnnouncementRepository _chatGroupAnnouncementRepository;

    public GetGroupAnnouncementsQueryHandler(
        IChatGroupRepository chatGroupRepository, IChatGroupAnnouncementRepository chatGroupAnnouncementRepository)
    {
        _chatGroupRepository = chatGroupRepository;
        _chatGroupAnnouncementRepository = chatGroupAnnouncementRepository;
    }

    /// <summary>
    /// Empty for anybody not in the group, which is the same answer they get about its messages - see
    /// IChatGroupRepository on why a group you are not in and a group that does not exist read alike.
    /// </summary>
    public async Task<IReadOnlyList<ChatGroupAnnouncement>> HandleAsync(
        GetGroupAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        var group = await _chatGroupRepository.GetByIdAsync(request.GroupId, cancellationToken);
        if (group is null || !group.IsMember(request.UserId))
        {
            return [];
        }

        return await _chatGroupAnnouncementRepository.GetForGroupAsync(request.GroupId, request.SinceUtc, cancellationToken);
    }
}
