using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Crypto;

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
            Email = contact.Email,
            PublicKeyBase64 = contact.PublicKeyBase64,
            LastMessageAtUtc = contact.LastMessageAtUtc,
            RequiresApprovalFromCurrentUser = contact.RequiresApprovalFromCurrentUser,
            IsPendingApprovalFromOtherParty = contact.IsPendingApprovalFromOtherParty,
            PresenceStatus = contact.PresenceStatus,
            IsArchived = contact.IsArchived
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The groups this phone knows about, newest first.</summary>
    public async Task<IReadOnlyList<LocalChatGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ChatGroups
            .AsNoTracking()
            .OrderByDescending(group => group.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<LocalChatGroup?> FindGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ChatGroups.AsNoTracking().FirstOrDefaultAsync(group => group.Id == groupId, cancellationToken);
    }

    /// <summary>
    /// Replaces the cached group list wholesale, for the same reason contacts are replaced rather than
    /// merged: the server's list is the complete answer, and a group the user was removed from should
    /// disappear here too.
    /// </summary>
    public async Task StoreGroupsAsync(IReadOnlyList<LocalChatGroup> groups, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.ChatGroups.ExecuteDeleteAsync(cancellationToken);
        dbContext.ChatGroups.AddRange(groups);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LocalChatMessage>> GetGroupConversationAsync(
        Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ChatMessages
            .AsNoTracking()
            .Where(message => message.GroupId == groupId)
            .OrderBy(message => message.SentAtUtc)
            .ToListAsync(cancellationToken);
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
    /// Drops what this phone had cached of one conversation, after the server was told to empty it -
    /// see ChatClient.ClearConversationHistoryAsync. Without this the words would still be here: a pull
    /// only ever adds, and the server has nothing left to send that would take them away.
    /// </summary>
    public async Task DeleteConversationAsync(Guid otherUserId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.ChatMessages
            .Where(message => message.OtherUserId == otherUserId && message.GroupId == null)
            .ExecuteDeleteAsync(cancellationToken);
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
    /// <returns>
    /// How many rows this actually wrote, which is not the number handed in. The group endpoint returns
    /// the whole conversation every time, so a caller that treated "messages came back" as "something
    /// changed" would rebuild the screen on every poll.
    /// </returns>
    public Task<int> StoreAsync(
        Guid otherUserId, IReadOnlyList<ChatMessageDto> messages, CancellationToken cancellationToken = default)
        => StoreAsync(messages, message => message.OtherUserId = otherUserId, cancellationToken);

    /// <inheritdoc cref="StoreAsync(Guid, IReadOnlyList{ChatMessageDto}, CancellationToken)"/>
    public Task<int> StoreGroupMessagesAsync(
        Guid groupId, IReadOnlyList<ChatMessageDto> messages, CancellationToken cancellationToken = default)
        => StoreAsync(messages, message => message.GroupId = groupId, cancellationToken);

    /// <param name="address">
    /// Sets whichever conversation the message belongs to. The two kinds are keyed differently - a person
    /// for one-to-one, a group otherwise - and nothing else about storing them differs.
    /// </param>
    private async Task<int> StoreAsync(
        IReadOnlyList<ChatMessageDto> messages, Action<LocalChatMessage> address, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return 0;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var incoming in messages)
        {
            var existing = await dbContext.ChatMessages.FirstOrDefaultAsync(
                message => message.Id == incoming.Id, cancellationToken);

            var message = existing ?? Add(dbContext, incoming.Id);
            address(message);
            message.SenderUserId = incoming.SenderUserId;
            message.RecipientUserId = incoming.RecipientUserId;
            message.GroupMessageId = incoming.GroupMessageId;
            message.CiphertextBase64 = incoming.CiphertextBase64;
            message.NonceBase64 = incoming.NonceBase64;
            message.SentAtUtc = incoming.SentAtUtc;
            message.IsEdited = incoming.IsEdited;
            message.IsReadByEveryone = incoming.ReadByEveryone;
        }

        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Replaces one message's ciphertext with a freshly sealed one, and marks it as edited. Done here as
    /// well as on the server so the conversation shows the new words at once: a one-to-one pull only asks
    /// for what is newer, so an edit to something older would not come back on its own.
    /// </summary>
    public async Task RewriteAsync(Guid messageId, EncryptedText sealedText, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.ChatMessages.FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken) is not { } stored)
        {
            return;
        }

        stored.CiphertextBase64 = sealedText.CiphertextBase64;
        stored.NonceBase64 = sealedText.NonceBase64;
        stored.IsEdited = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Drops a message this phone holds. A group copy takes every copy of the same posting with it, which
    /// is what the server does too - a message leaves the group rather than one member's view of it.
    /// </summary>
    public async Task ForgetAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.ChatMessages.FirstOrDefaultAsync(message => message.Id == messageId, cancellationToken) is not { } stored)
        {
            return;
        }

        if (stored.GroupMessageId is { } groupMessageId)
        {
            await dbContext.ChatMessages
                .Where(message => message.GroupMessageId == groupMessageId)
                .ExecuteDeleteAsync(cancellationToken);
            return;
        }

        dbContext.ChatMessages.Remove(stored);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Drops every copy of one group posting. An edited group message is re-sealed per member, so the
    /// copies that come back carry new ids - without this the old ones would sit alongside them.
    /// </summary>
    public async Task ForgetGroupMessageAsync(Guid groupMessageId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.ChatMessages
            .Where(message => message.GroupMessageId == groupMessageId)
            .ExecuteDeleteAsync(cancellationToken);
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

    public async Task<IReadOnlyList<OutgoingChatMessage>> GetQueuedForGroupAsync(
        Guid groupId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.OutgoingChatMessages
            .AsNoTracking()
            .Where(message => message.GroupId == groupId)
            .OrderBy(message => message.Id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Queues text to send. Not encrypted here - see <see cref="OutgoingChatMessage"/>.</summary>
    public Task<OutgoingChatMessage> QueueAsync(
        Guid recipientUserId, string text, CancellationToken cancellationToken = default)
        => QueueAsync(new OutgoingChatMessage { RecipientUserId = recipientUserId, Text = text }, cancellationToken);

    /// <inheritdoc cref="QueueAsync(Guid, string, CancellationToken)"/>
    public Task<OutgoingChatMessage> QueueForGroupAsync(
        Guid groupId, string text, CancellationToken cancellationToken = default)
        => QueueAsync(new OutgoingChatMessage { GroupId = groupId, Text = text }, cancellationToken);

    private async Task<OutgoingChatMessage> QueueAsync(OutgoingChatMessage queued, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        queued.QueuedAtUtc = _timeProvider.GetUtcNow();

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
