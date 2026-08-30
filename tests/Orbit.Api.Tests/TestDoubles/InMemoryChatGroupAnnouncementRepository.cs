using Orbit.Core.Chat.Groups;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IChatGroupAnnouncementRepository"/> for tests that need the "somebody joined"
/// lines a group accumulates, without a database behind them.
/// </summary>
internal sealed class InMemoryChatGroupAnnouncementRepository : IChatGroupAnnouncementRepository
{
    private readonly List<ChatGroupAnnouncement> _announcements = [];

    /// <summary>Everything stored, for tests that assert on the rows rather than on a query's answer.</summary>
    public IReadOnlyList<ChatGroupAnnouncement> All => _announcements;

    public Task AddAsync(ChatGroupAnnouncement announcement, CancellationToken cancellationToken)
    {
        _announcements.Add(announcement);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChatGroupAnnouncement>> GetForGroupAsync(
        Guid groupId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        var announcements = _announcements.Where(announcement => announcement.GroupId == groupId);

        if (sinceUtc is not null)
        {
            announcements = announcements.Where(announcement => announcement.AnnouncedAtUtc > sinceUtc.Value);
        }

        return Task.FromResult<IReadOnlyList<ChatGroupAnnouncement>>(
            announcements.OrderBy(announcement => announcement.AnnouncedAtUtc).ToList());
    }

    public Task<ChatGroupAnnouncement?> FindLatestJoinAsync(Guid groupId, Guid joinedUserId, CancellationToken cancellationToken)
        => Task.FromResult(
            _announcements
                .Where(announcement => announcement.GroupId == groupId && announcement.JoinedUserId == joinedUserId)
                .OrderByDescending(announcement => announcement.AnnouncedAtUtc)
                .FirstOrDefault());

    /// <summary>
    /// A no-op beyond confirming the row is known: the handler mutates the same instance this hands out,
    /// which is what an in-memory store gives it back.
    /// </summary>
    public Task UpdateAsync(ChatGroupAnnouncement announcement, CancellationToken cancellationToken)
    {
        var stored = _announcements.FindIndex(candidate => candidate.Id == announcement.Id);
        if (stored >= 0)
        {
            _announcements[stored] = announcement;
        }

        return Task.CompletedTask;
    }
}
