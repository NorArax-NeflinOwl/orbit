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
        // GroupId == null keeps group messages out of the one-to-one conversation. A group message is
        // sealed pairwise, one copy per member (see ChatMessage.CreateForGroup), so in a two-person group
        // a copy has exactly the same sender/recipient pair as a one-to-one message between the two - and
        // without this clause it would surface in their one-to-one thread. Groups are read by GroupId.
        var query = _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message =>
                message.GroupId == null &&
                ((message.SenderUserId == userId && message.RecipientUserId == otherUserId) ||
                 (message.SenderUserId == otherUserId && message.RecipientUserId == userId)));

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

    /// <summary>
    /// The ciphertext and the nonce go with the flag, in one statement: a row still holding readable
    /// bytes is not deleted whatever it is marked, and two statements would leave a window where it was
    /// marked and still readable.
    /// </summary>
    public async Task MarkDeletedAsync(
        Guid messageId, Guid deletedByUserId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken)
    {
        await _dbContext.ChatMessages
            .Where(message => message.Id == messageId && message.DeletedAtUtc == null)
            .ExecuteUpdateAsync(
                message => message
                    .SetProperty(stored => stored.CiphertextBase64, string.Empty)
                    .SetProperty(stored => stored.NonceBase64, string.Empty)
                    .SetProperty(stored => stored.DeletedAtUtc, deletedAtUtc)
                    .SetProperty(stored => stored.DeletedByUserId, deletedByUserId),
                cancellationToken);
    }

    /// <summary>
    /// The copies of this group's messages addressed to one member, and only those - what they take
    /// with them when they leave. Copies addressed to anybody else stay, including ones this member
    /// sent: those are the others' to read.
    /// </summary>
    public async Task DeleteGroupCopiesForAsync(Guid groupId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        await _dbContext.ChatMessages
            .Where(message => message.GroupId == groupId && message.RecipientUserId == recipientUserId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Read first, then written: ExecuteUpdateAsync says how many rows it changed but not which, and
    /// who was in those conversations is the answer the caller needs. The read is by the same predicate,
    /// so a message that arrives between the two is simply not part of this withdrawal.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> MarkShareAnnouncementsDeletedAsync(
        Guid shareId, Guid deletedByUserId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken)
    {
        var announcements = _dbContext.ChatMessages
            .Where(message => message.AnnouncesShareId == shareId && message.DeletedAtUtc == null);

        var everybodyInvolved = await announcements
            .AsNoTracking()
            .Select(message => new { message.SenderUserId, message.RecipientUserId })
            .ToListAsync(cancellationToken);

        await announcements.ExecuteUpdateAsync(
            message => message
                .SetProperty(stored => stored.CiphertextBase64, string.Empty)
                .SetProperty(stored => stored.NonceBase64, string.Empty)
                .SetProperty(stored => stored.DeletedAtUtc, deletedAtUtc)
                .SetProperty(stored => stored.DeletedByUserId, deletedByUserId),
            cancellationToken);

        return everybodyInvolved
            .SelectMany(conversation => new[] { conversation.SenderUserId, conversation.RecipientUserId })
            .Distinct()
            .ToList();
    }

    /// <inheritdoc cref="MarkDeletedAsync"/>
    public async Task MarkGroupMessageDeletedAsync(
        Guid groupMessageId, Guid deletedByUserId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken)
    {
        await _dbContext.ChatMessages
            .Where(message => message.GroupMessageId == groupMessageId && message.DeletedAtUtc == null)
            .ExecuteUpdateAsync(
                message => message
                    .SetProperty(stored => stored.CiphertextBase64, string.Empty)
                    .SetProperty(stored => stored.NonceBase64, string.Empty)
                    .SetProperty(stored => stored.DeletedAtUtc, deletedAtUtc)
                    .SetProperty(stored => stored.DeletedByUserId, deletedByUserId),
                cancellationToken);
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

    public async Task<bool> MarkConversationAsReadAsync(
        Guid readerUserId, Guid otherUserId, DateTimeOffset readAtUtc, DateTimeOffset? readUpToUtc,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ChatMessages
            .Where(message =>
                message.SenderUserId == otherUserId && message.RecipientUserId == readerUserId && message.ReadAtUtc == null);

        if (readUpToUtc is not null)
        {
            query = query.Where(message => message.SentAtUtc <= readUpToUtc.Value);
        }

        var unreadEntities = await query.ToListAsync(cancellationToken);

        if (unreadEntities.Count == 0)
        {
            return false;
        }

        foreach (var entity in unreadEntities)
        {
            entity.ReadAtUtc = readAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
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
            entity.IsEdited, entity.EditedAtUtc, entity.GroupId, entity.GroupMessageId, entity.IsSharedHistory,
            entity.DeletedAtUtc, entity.DeletedByUserId, entity.AnnouncesShareId);

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
            IsSharedHistory = message.IsSharedHistory,
            AnnouncesShareId = message.AnnouncesShareId
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
    public async Task<IReadOnlyDictionary<Guid, int>> GetGroupUnreadCountsAsync(
        Guid readerUserId, CancellationToken cancellationToken)
    {
        // The reader's own copies only - a sender gets none of their own post, so it never counts. A
        // deleted message is left out too: it is not there to be read, and "exactly how many arrived" is
        // the number of messages somebody can still open.
        var counts = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message =>
                message.RecipientUserId == readerUserId
                && message.ReadAtUtc == null
                && message.GroupId != null
                && !message.IsSharedHistory
                && message.DeletedAtUtc == null)
            .GroupBy(message => message.GroupId!.Value)
            .Select(byGroup => new { GroupId = byGroup.Key, Count = byGroup.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(entry => entry.GroupId, entry => entry.Count);
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
    public async Task<bool> MarkGroupConversationAsReadAsync(
        Guid readerUserId, Guid groupId, DateTimeOffset readAtUtc, DateTimeOffset? readUpToUtc,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ChatMessages
            .Where(message =>
                message.GroupId == groupId && message.RecipientUserId == readerUserId && message.ReadAtUtc == null);

        if (readUpToUtc is not null)
        {
            query = query.Where(message => message.SentAtUtc <= readUpToUtc.Value);
        }

        // The row count is the answer: ExecuteUpdate hands back how many it touched, which is exactly
        // "was there anything to read" without a second query for it.
        var marked = await query
            .ExecuteUpdateAsync(update => update.SetProperty(message => message.ReadAtUtc, readAtUtc), cancellationToken);

        return marked > 0;
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