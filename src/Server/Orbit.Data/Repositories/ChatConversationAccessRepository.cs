using Microsoft.EntityFrameworkCore;
using Orbit.Core.Chat;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class ChatConversationAccessRepository : IChatConversationAccessRepository
{
    private readonly OrbitDbContext _dbContext;

    public ChatConversationAccessRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ChatConversationAccess?> GetAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        var entity = await FindEntityAsync(userId, otherUserId, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task EnsureCreatedAsync(Guid initiatedByUserId, Guid otherUserId, CancellationToken cancellationToken)
    {
        if (await FindEntityAsync(initiatedByUserId, otherUserId, cancellationToken) is not null)
        {
            return;
        }

        _dbContext.ChatConversationAccesses.Add(ToEntity(ChatConversationAccess.Create(initiatedByUserId, otherUserId)));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ApproveAsync(Guid approvingUserId, Guid otherUserId, CancellationToken cancellationToken)
    {
        var entity = await FindEntityAsync(approvingUserId, otherUserId, cancellationToken);
        if (entity is null || entity.InitiatedByUserId == approvingUserId)
        {
            return false;
        }

        var access = ToDomain(entity);
        access.Approve();
        entity.ApprovedAtUtc = access.ApprovedAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// A pair is stored as a single, direction-agnostic row (see OrbitDbContext's index), so every
    /// lookup has to check both orderings of who's "initiator" and who's "other party".
    /// </summary>
    private Task<ChatConversationAccessEntity?> FindEntityAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
        => _dbContext.ChatConversationAccesses.FirstOrDefaultAsync(entity =>
            (entity.InitiatedByUserId == userId && entity.OtherUserId == otherUserId) ||
            (entity.InitiatedByUserId == otherUserId && entity.OtherUserId == userId), cancellationToken);

    private static ChatConversationAccess ToDomain(ChatConversationAccessEntity entity)
        => ChatConversationAccess.FromPersistence(entity.Id, entity.InitiatedByUserId, entity.OtherUserId, entity.CreatedAtUtc, entity.ApprovedAtUtc);

    private static ChatConversationAccessEntity ToEntity(ChatConversationAccess access)
        => new()
        {
            Id = access.Id,
            InitiatedByUserId = access.InitiatedByUserId,
            OtherUserId = access.OtherUserId,
            CreatedAtUtc = access.CreatedAtUtc,
            ApprovedAtUtc = access.ApprovedAtUtc
        };
}
