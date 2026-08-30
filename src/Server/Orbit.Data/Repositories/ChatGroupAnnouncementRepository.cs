using Microsoft.EntityFrameworkCore;
using Orbit.Core.Chat.Groups;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class ChatGroupAnnouncementRepository : IChatGroupAnnouncementRepository
{
    private readonly OrbitDbContext _dbContext;

    public ChatGroupAnnouncementRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ChatGroupAnnouncement announcement, CancellationToken cancellationToken)
    {
        _dbContext.ChatGroupAnnouncements.Add(ToEntity(announcement));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatGroupAnnouncement>> GetForGroupAsync(
        Guid groupId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        var query = _dbContext.ChatGroupAnnouncements
            .AsNoTracking()
            .Where(announcement => announcement.GroupId == groupId);

        if (sinceUtc is not null)
        {
            query = query.Where(announcement => announcement.AnnouncedAtUtc > sinceUtc.Value);
        }

        var entities = await query.OrderBy(announcement => announcement.AnnouncedAtUtc).ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    public async Task<ChatGroupAnnouncement?> FindLatestJoinAsync(
        Guid groupId, Guid joinedUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ChatGroupAnnouncements
            .AsNoTracking()
            .Where(announcement => announcement.GroupId == groupId && announcement.JoinedUserId == joinedUserId)
            .OrderByDescending(announcement => announcement.AnnouncedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(ChatGroupAnnouncement announcement, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ChatGroupAnnouncements
            .FirstOrDefaultAsync(stored => stored.Id == announcement.Id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.HistoryShared = announcement.HistoryShared;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ChatGroupAnnouncement ToDomain(ChatGroupAnnouncementEntity entity)
        => ChatGroupAnnouncement.FromPersistence(
            entity.Id, entity.GroupId, entity.JoinedUserId, entity.AddedByUserId, entity.HistoryShared, entity.AnnouncedAtUtc);

    private static ChatGroupAnnouncementEntity ToEntity(ChatGroupAnnouncement announcement)
        => new()
        {
            Id = announcement.Id,
            GroupId = announcement.GroupId,
            JoinedUserId = announcement.JoinedUserId,
            AddedByUserId = announcement.AddedByUserId,
            HistoryShared = announcement.HistoryShared,
            AnnouncedAtUtc = announcement.AnnouncedAtUtc
        };
}
