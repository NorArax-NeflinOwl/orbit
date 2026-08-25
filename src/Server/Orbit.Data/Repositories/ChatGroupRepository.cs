using Microsoft.EntityFrameworkCore;
using Orbit.Core.Chat.Groups;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class ChatGroupRepository : IChatGroupRepository
{
    private readonly OrbitDbContext _dbContext;

    public ChatGroupRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ChatGroup group, CancellationToken cancellationToken)
    {
        _dbContext.ChatGroups.Add(ToEntity(group));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ChatGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ChatGroups
            .AsNoTracking()
            .Include(group => group.Members)
            .FirstOrDefaultAsync(group => group.Id == groupId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<ChatGroup>> GetForMemberAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.ChatGroups
            .AsNoTracking()
            .Include(group => group.Members)
            .Where(group => group.Members.Any(member => member.UserId == userId))
            .OrderByDescending(group => group.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    /// <summary>
    /// Replaces the whole membership list rather than diffing it, mirroring how a task list's items are
    /// saved: the domain hands back the membership as it should now stand, and working out which single
    /// row changed would only be a way to get it wrong.
    /// </summary>
    public async Task UpdateAsync(ChatGroup group, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ChatGroups
            .Include(candidate => candidate.Members)
            .FirstAsync(candidate => candidate.Id == group.Id, cancellationToken);

        entity.Name = group.Name;
        _dbContext.ChatGroupMembers.RemoveRange(entity.Members);
        _dbContext.ChatGroupMembers.AddRange(group.Members.Select(member => ToMemberEntity(member)));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ChatGroup ToDomain(ChatGroupEntity entity)
        => ChatGroup.FromPersistence(
            entity.Id, entity.Name, entity.CreatedByUserId, entity.CreatedAtUtc,
            entity.Members
                .Select(member => new ChatGroupMembership(
                    member.GroupId, member.UserId, Enum.Parse<ChatGroupRole>(member.Role, ignoreCase: true), member.JoinedAtUtc))
                .ToList());

    private static ChatGroupEntity ToEntity(ChatGroup group)
        => new()
        {
            Id = group.Id,
            Name = group.Name,
            CreatedByUserId = group.CreatedByUserId,
            CreatedAtUtc = group.CreatedAtUtc,
            Members = group.Members.Select(ToMemberEntity).ToList()
        };

    private static ChatGroupMemberEntity ToMemberEntity(ChatGroupMembership membership)
        => new()
        {
            Id = Guid.NewGuid(),
            GroupId = membership.GroupId,
            UserId = membership.UserId,
            Role = membership.Role.ToString(),
            JoinedAtUtc = membership.JoinedAtUtc
        };
}
