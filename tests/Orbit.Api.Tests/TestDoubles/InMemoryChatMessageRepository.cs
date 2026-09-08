using Orbit.Core.Chat;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IChatMessageRepository"/> stub for unit tests that need real add/lookup
/// behavior, including both-directions conversation scoping, without spinning up SQLite.
/// </summary>
internal sealed class InMemoryChatMessageRepository : IChatMessageRepository
{
    private readonly List<ChatMessage> _messages = [];

    /// <summary>Everything stored, for tests that assert on the rows themselves rather than a query's answer.</summary>
    public IReadOnlyList<ChatMessage> All => _messages;

    /// <summary>
    /// Read state lives here instead of on <see cref="ChatMessage"/> itself, mirroring how the real
    /// repository tracks it on ChatMessageEntity.ReadAtUtc without the domain object needing to know
    /// about it.
    /// </summary>
    private readonly Dictionary<Guid, DateTimeOffset> _readAtUtcByMessageId = [];

    public Task<IReadOnlyList<ChatMessage>> GetConversationAsync(
        Guid userId, Guid otherUserId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        // Mirrors the real repository: a one-to-one conversation excludes group messages, whose pairwise
        // copies would otherwise match this sender/recipient filter in a two-person group.
        var messages = _messages.Where(message =>
            message.GroupId is null &&
            ((message.SenderUserId == userId && message.RecipientUserId == otherUserId) ||
             (message.SenderUserId == otherUserId && message.RecipientUserId == userId)));

        if (sinceUtc is not null)
        {
            messages = messages.Where(message => message.SentAtUtc > sinceUtc.Value);
        }

        return Task.FromResult<IReadOnlyList<ChatMessage>>(messages.OrderBy(message => message.SentAtUtc).ToList());
    }

    public Task AddAsync(ChatMessage message, CancellationToken cancellationToken)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken)
        => Task.FromResult(_messages.FirstOrDefault(message => message.Id == messageId));

    public Task UpdateContentAsync(
        Guid messageId, string ciphertextBase64, string nonceBase64, DateTimeOffset editedAtUtc, CancellationToken cancellationToken)
    {
        _messages.FirstOrDefault(message => message.Id == messageId)?.ApplyEdit(ciphertextBase64, nonceBase64, editedAtUtc);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Answers whether anything was actually marked, the way the real one does - which is what the
    /// handler publishes on, and so the difference between a read receipt and two windows announcing at
    /// each other. See MarkConversationAsReadCommandHandler.
    /// </summary>
    public Task<bool> MarkConversationAsReadAsync(
        Guid readerUserId, Guid otherUserId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
    {
        var anythingWasUnread = false;
        foreach (var message in _messages)
        {
            var isUnreadFromOtherParty =
                message.SenderUserId == otherUserId && message.RecipientUserId == readerUserId && !_readAtUtcByMessageId.ContainsKey(message.Id);
            if (isUnreadFromOtherParty)
            {
                _readAtUtcByMessageId[message.Id] = readAtUtc;
                anythingWasUnread = true;
            }
        }

        return Task.FromResult(anythingWasUnread);
    }

    public Task<DateTimeOffset?> GetReadUpToUtcAsync(Guid senderUserId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var readSentTimestamps = _messages
            .Where(message =>
                message.SenderUserId == senderUserId && message.RecipientUserId == recipientUserId &&
                _readAtUtcByMessageId.ContainsKey(message.Id))
            .Select(message => message.SentAtUtc)
            .ToList();

        return Task.FromResult(readSentTimestamps.Count == 0 ? null : (DateTimeOffset?)readSentTimestamps.Max());
    }

    public Task<IReadOnlyList<ChatMessage>> GetGroupConversationAsync(
        Guid groupId, Guid userId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        var messages = _messages.Where(message =>
            message.GroupId == groupId && (message.SenderUserId == userId || message.RecipientUserId == userId));

        if (sinceUtc is not null)
        {
            messages = messages.Where(message => message.SentAtUtc > sinceUtc.Value);
        }

        return Task.FromResult<IReadOnlyList<ChatMessage>>(messages.OrderBy(message => message.SentAtUtc).ToList());
    }

    /// <summary>
    /// Keeps the row and empties it, which is what the real one does - see ChatMessage.Delete. A double
    /// that removed the row instead would let a handler pass here and leave a hole in production, which
    /// is the failure this project has already been bitten by twice (see the memory on test doubles
    /// refusing what the server refuses).
    /// </summary>
    public Task MarkDeletedAsync(
        Guid messageId, Guid deletedByUserId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken)
    {
        foreach (var message in _messages.Where(message => message.Id == messageId))
        {
            message.Delete(deletedByUserId, deletedAtUtc);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc cref="MarkDeletedAsync"/>
    public Task MarkGroupMessageDeletedAsync(
        Guid groupMessageId, Guid deletedByUserId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken)
    {
        foreach (var message in _messages.Where(message => message.GroupMessageId == groupMessageId))
        {
            message.Delete(deletedByUserId, deletedAtUtc);
        }

        return Task.CompletedTask;
    }

    public Task DeleteGroupCopiesForAsync(Guid groupId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        _messages.RemoveAll(message => message.GroupId == groupId && message.RecipientUserId == recipientUserId);
        return Task.CompletedTask;
    }
    public Task<IReadOnlyDictionary<Guid, int>> GetUnreadCountsBySenderAsync(
        Guid readerUserId, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, int> counts = _messages
            .Where(message =>
                message.RecipientUserId == readerUserId
                && !_readAtUtcByMessageId.ContainsKey(message.Id)
                && message.GroupId is null)
            .GroupBy(message => message.SenderUserId)
            .ToDictionary(bySender => bySender.Key, bySender => bySender.Count());

        return Task.FromResult(counts);
    }
    public Task<IReadOnlyList<ChatMessage>> GetGroupMessageCopiesAsync(
        Guid groupMessageId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ChatMessage>>(
            _messages.Where(message => message.GroupMessageId == groupMessageId).ToList());
    /// <summary>The group's own, and the same answer - see MarkConversationAsReadAsync above.</summary>
    public Task<bool> MarkGroupConversationAsReadAsync(
        Guid readerUserId, Guid groupId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
    {
        var anythingWasUnread = false;
        foreach (var message in _messages.Where(message =>
                     message.GroupId == groupId && message.RecipientUserId == readerUserId))
        {
            anythingWasUnread |= _readAtUtcByMessageId.TryAdd(message.Id, readAtUtc);
        }

        return Task.FromResult(anythingWasUnread);
    }

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<GroupMessageReceipt>>> GetGroupReceiptsAsync(
        IReadOnlyCollection<Guid> groupMessageIds, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<GroupMessageReceipt>> byMessage = _messages
            .Where(message => message.GroupMessageId is { } id && groupMessageIds.Contains(id) && !message.IsSharedHistory)
            .GroupBy(message => message.GroupMessageId!.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<GroupMessageReceipt>)group
                    .Select(message => new GroupMessageReceipt(
                        message.RecipientUserId,
                        _readAtUtcByMessageId.TryGetValue(message.Id, out var readAt) ? readAt : null))
                    .ToList());

        return Task.FromResult(byMessage);
    }
}