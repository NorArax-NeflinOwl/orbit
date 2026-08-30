namespace Orbit.Core.Chat.Groups;

public interface IChatGroupAnnouncementRepository
{
    Task AddAsync(ChatGroupAnnouncement announcement, CancellationToken cancellationToken);

    /// <summary>
    /// A group's announcements, oldest first. sinceUtc mirrors the conversation's own cursor so a client
    /// polling both asks each the same question - see GetGroupConversationQuery.
    /// </summary>
    Task<IReadOnlyList<ChatGroupAnnouncement>> GetForGroupAsync(
        Guid groupId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken);

    /// <summary>
    /// The most recent announcement of this person joining this group, or null if there is none - what
    /// a history share attaches itself to. Most recent rather than only: somebody can be added, leave,
    /// and be added again, and it is the current arrival the share belongs to.
    /// </summary>
    Task<ChatGroupAnnouncement?> FindLatestJoinAsync(Guid groupId, Guid joinedUserId, CancellationToken cancellationToken);

    Task UpdateAsync(ChatGroupAnnouncement announcement, CancellationToken cancellationToken);
}
