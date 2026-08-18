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
        // Only the SenderUserId/RecipientUserId filter is translated to SQL. sinceUtc filtering and the
        // final ordering both operate on the DateTimeOffset column, which SQLite can't translate (see the
        // EF Core exception this avoids, and GetReadUpToUtcAsync below for the same limitation) - so both
        // happen in memory after fetching the conversation instead.
        var entities = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(message =>
                (message.SenderUserId == userId && message.RecipientUserId == otherUserId) ||
                (message.SenderUserId == otherUserId && message.RecipientUserId == userId))
            .ToListAsync(cancellationToken);

        var messagesSinceUtc = sinceUtc is null
            ? entities
            : entities.Where(entity => entity.SentAtUtc > sinceUtc.Value);

        return messagesSinceUtc
            .OrderBy(entity => entity.SentAtUtc)
            .Select(ToDomain)
            .ToList();
    }

    public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        _dbContext.ChatMessages.Add(ToEntity(message));
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
            entity.Id, entity.SenderUserId, entity.RecipientUserId, entity.CiphertextBase64, entity.NonceBase64, entity.SentAtUtc);

    private static ChatMessageEntity ToEntity(ChatMessage message)
        => new()
        {
            Id = message.Id,
            SenderUserId = message.SenderUserId,
            RecipientUserId = message.RecipientUserId,
            CiphertextBase64 = message.CiphertextBase64,
            NonceBase64 = message.NonceBase64,
            SentAtUtc = message.SentAtUtc
        };
}
