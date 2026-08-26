using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Chat;

namespace Orbit.Mobile.Data;

/// <summary>
/// The phone's copy of chat: the messages it has seen, and the ones it has been asked to send but
/// hasn't yet. Everything a conversation screen reads comes from here, so history is readable with no
/// connection.
/// </summary>
public sealed class ChatRepository
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public ChatRepository(IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
    }

    /// <summary>The people this phone knows about, most recently spoken to first.</summary>
    public async Task<IReadOnlyList<LocalContact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Contacts
            .AsNoTracking()
            .OrderByDescending(contact => contact.LastMessageAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Replaces the cached contact list with what the server just returned. A wholesale replace rather
    /// than a merge, because the server's list is the complete answer - someone who has dropped off it
    /// should drop off here too.
    /// </summary>
    public async Task StoreContactsAsync(IReadOnlyList<ContactDto> contacts, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Contacts.ExecuteDeleteAsync(cancellationToken);

        dbContext.Contacts.AddRange(contacts.Select(contact => new LocalContact
        {
            UserId = contact.UserId,
            UserName = contact.UserName,
            DisplayName = contact.DisplayName,
            PublicKeyBase64 = contact.PublicKeyBase64,
            LastMessageAtUtc = contact.LastMessageAtUtc,
            RequiresApprovalFromCurrentUser = contact.RequiresApprovalFromCurrentUser,
            IsPendingApprovalFromOtherParty = contact.IsPendingApprovalFromOtherParty
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LocalChatMessage>> GetConversationAsync(
        Guid otherUserId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.OtherUserId == otherUserId)
            .OrderBy(message => message.SentAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// When the newest message in this conversation arrived, so the next pull can ask for only what came
    /// after it. Null when the phone has never seen this conversation.
    /// </summary>
    public async Task<DateTimeOffset?> LatestMessageAtAsync(Guid otherUserId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var conversation = dbContext.ChatMessages.Where(message => message.OtherUserId == otherUserId);
        return await conversation.AnyAsync(cancellationToken)
            ? await conversation.MaxAsync(message => message.SentAtUtc, cancellationToken)
            : null;
    }

    /// <summary>
    /// Stores what the server sent, replacing anything already held for the same message - an edited
    /// message comes back under the same id with new ciphertext.
    /// </summary>
    public async Task StoreAsync(
        Guid otherUserId, IReadOnlyList<ChatMessageDto> messages, CancellationToken cancellationToken = default)
    {
        if (messages.Count == 0)
        {
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var incoming in messages)
        {
            var existing = await dbContext.ChatMessages.FirstOrDefaultAsync(
                message => message.Id == incoming.Id, cancellationToken);

            var message = existing ?? Add(dbContext, incoming.Id);
            message.OtherUserId = otherUserId;
            message.SenderUserId = incoming.SenderUserId;
            message.CiphertextBase64 = incoming.CiphertextBase64;
            message.NonceBase64 = incoming.NonceBase64;
            message.SentAtUtc = incoming.SentAtUtc;
            message.IsEdited = incoming.IsEdited;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Everything waiting to go out, oldest first - the order it must be sent in.</summary>
    public async Task<IReadOnlyList<OutgoingChatMessage>> GetQueuedAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.OutgoingChatMessages.OrderBy(message => message.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutgoingChatMessage>> GetQueuedForAsync(
        Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.OutgoingChatMessages
            .AsNoTracking()
            .Where(message => message.RecipientUserId == recipientUserId)
            .OrderBy(message => message.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Queues text to send. Not encrypted here - see <see cref="OutgoingChatMessage"/>.</summary>
    public async Task<OutgoingChatMessage> QueueAsync(
        Guid recipientUserId, string text, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var queued = new OutgoingChatMessage
        {
            RecipientUserId = recipientUserId,
            Text = text,
            QueuedAtUtc = _timeProvider.GetUtcNow()
        };

        dbContext.OutgoingChatMessages.Add(queued);
        await dbContext.SaveChangesAsync(cancellationToken);
        return queued;
    }

    public async Task RemoveQueuedAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.OutgoingChatMessages.Where(message => message.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task RecordFailedAttemptAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.OutgoingChatMessages.FirstOrDefaultAsync(message => message.Id == id, cancellationToken) is { } queued)
        {
            queued.FailedAttempts++;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static LocalChatMessage Add(OrbitLocalDbContext dbContext, Guid id)
    {
        var message = new LocalChatMessage { Id = id };
        dbContext.ChatMessages.Add(message);
        return message;
    }
}
