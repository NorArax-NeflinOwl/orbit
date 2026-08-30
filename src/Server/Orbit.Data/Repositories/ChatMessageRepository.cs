using Microsoft.EntityFrameworkCore;
using Orbit.Core.Chat;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class ChatMessageRepository : IChatMessageRepository
{
    private readonly OrbitDbContext _dbContext;

    public ChatMessageRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetConversationAsync(
        Guid userId, Guid otherUserId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        // Filtered and ordered in the database. This used to fetch the whole conversation and narrow it
        // here, because SQLite could not translate a comparison on a DateTimeOffset column - a real
        // limitation of a provider this app no longer uses. Against PostgreSQL the column is a
        // timestamptz and Npgsql translates both, so a chat window polling once a second stopped asking
        // for its entire history on every tick.
        var query = _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message =>
                (message.SenderUserId == userId && message.RecipientUserId == otherUserId) ||
                (message.SenderUserId == otherUserId && message.RecipientUserId == userId));

        if (sinceUtc is not null)
        {
            query = query.Where(message => message.SentAtUtc > sinceUtc.Value);
        }

        var entities = await query.OrderBy(message => message.SentAtUtc).ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        _dbContext.ChatMessages.Add(ToEntity(message));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetGroupConversationAsync(
        Guid groupId, Guid userId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        var query = _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.GroupId == groupId && (message.SenderUserId == userId || message.RecipientUserId == userId));

        if (sinceUtc is not null)
        {
            // Safe alongside the copy-collapsing in GetGroupConversationQueryHandler: every copy of one
            // group message is stamped with the same SentAtUtc when it is fanned out, so a cursor either
            // takes all of a message's copies or none, never a subset that would change which one is kept.
            query = query.Where(message => message.SentAtUtc > sinceUtc.Value);
        }

        var entities = await query.OrderBy(message => message.SentAtUtc).ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    public async Task DeleteAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await _dbContext.ChatMessages.Where(message => message.Id == messageId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteGroupMessageAsync(Guid groupMessageId, CancellationToken cancellationToken)
    {
        await _dbContext.ChatMessages.Where(message => message.GroupMessageId == groupMessageId).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ChatMessages.AsNoTracking().FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateContentAsync(
        Guid messageId, string ciphertextBase64, string nonceBase64, DateTimeOffset editedAtUtc, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ChatMessages.FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.CiphertextBase64 = ciphertextBase64;
        entity.NonceBase64 = nonceBase64;
        entity.IsEdited = true;
        entity.EditedAtUtc = editedAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkConversationAsReadAsync(
        Guid readerUserId, Guid otherUserId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
    {
        var unreadEntities = await _dbContext.ChatMessages
            .Where(message =>
                message.SenderUserId == otherUserId && message.RecipientUserId == readerUserId && message.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        if (unreadEntities.Count == 0)
        {
            return;
        }

        foreach (var entity in unreadEntities)
        {
            entity.ReadAtUtc = readAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> GetReadUpToUtcAsync(Guid senderUserId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        // Mirrors GetConversationAsync above: SQLite can't translate ordering/aggregation over a
        // DateTimeOffset column, so the max has to be computed in memory after fetching the matching
        // timestamps (a lightweight projection, not the full rows).
        var readSentTimestamps = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message =>
                message.SenderUserId == senderUserId && message.RecipientUserId == recipientUserId && message.ReadAtUtc != null)
            .Select(message => message.SentAtUtc)
            .ToListAsync(cancellationToken);

        return readSentTimestamps.Count == 0 ? null : readSentTimestamps.Max();
    }

    private static ChatMessage ToDomain(ChatMessageEntity entity)
        => ChatMessage.FromPersistence(
            entity.Id, entity.SenderUserId, entity.RecipientUserId, entity.CiphertextBase64, entity.NonceBase64, entity.SentAtUtc,
            entity.IsEdited, entity.EditedAtUtc, entity.GroupId, entity.GroupMessageId, entity.IsSharedHistory);

    private static ChatMessageEntity ToEntity(ChatMessage message)
        => new()
        {
            Id = message.Id,
            SenderUserId = message.SenderUserId,
            RecipientUserId = message.RecipientUserId,
            CiphertextBase64 = message.CiphertextBase64,
            NonceBase64 = message.NonceBase64,
            GroupId = message.GroupId,
            GroupMessageId = message.GroupMessageId,
            SentAtUtc = message.SentAtUtc,
            IsEdited = message.IsEdited,
            EditedAtUtc = message.EditedAtUtc,
            IsSharedHistory = message.IsSharedHistory
        };
    public async Task<IReadOnlyDictionary<Guid, int>> GetUnreadCountsBySenderAsync(
        Guid readerUserId, CancellationToken cancellationToken)
    {
        // GroupId == null keeps group traffic out: a group's unread state belongs to the group row, not
        // to the one-to-one conversation with whoever happened to post in it.
        var counts = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message =>
                message.RecipientUserId == readerUserId && message.ReadAtUtc == null && message.GroupId == null)
            .GroupBy(message => message.SenderUserId)
            .Select(bySender => new { SenderUserId = bySender.Key, Count = bySender.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.SenderUserId, entry => entry.Count);
    }
    public async Task<IReadOnlyList<ChatMessage>> GetGroupMessageCopiesAsync(
        Guid groupMessageId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.GroupMessageId == groupMessageId)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }
    public async Task MarkGroupConversationAsReadAsync(
        Guid readerUserId, Guid groupId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
    {
        await _dbContext.ChatMessages
            .Where(message =>
                message.GroupId == groupId && message.RecipientUserId == readerUserId && message.ReadAtUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(message => message.ReadAtUtc, readAtUtc), cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<GroupMessageReceipt>>> GetGroupReceiptsAsync(
        IReadOnlyCollection<Guid> groupMessageIds, CancellationToken cancellationToken)
    {
        if (groupMessageIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<GroupMessageReceipt>>();
        }

        // Copies re-encrypted for a later joiner are left out: they say nothing about whether the message
        // reached the people it was posted to, and counting them would turn a sender's fully-read message
        // back into an unread one the moment somebody was given the history.
        var rows = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message =>
                message.GroupMessageId != null && groupMessageIds.Contains(message.GroupMessageId.Value)
                && !message.IsSharedHistory)
            .Select(message => new { GroupMessageId = message.GroupMessageId!.Value, message.RecipientUserId, message.ReadAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.GroupMessageId)
            .ToDictionary(
                byMessage => byMessage.Key,
                byMessage => (IReadOnlyList<GroupMessageReceipt>)byMessage
                    .Select(row => new GroupMessageReceipt(row.RecipientUserId, row.ReadAtUtc))
                    .ToList());
    }
}