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
        var messages = _messages.Where(message =>
            (message.SenderUserId == userId && message.RecipientUserId == otherUserId) ||
            (message.SenderUserId == otherUserId && message.RecipientUserId == userId));

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

    public Task MarkConversationAsReadAsync(
        Guid readerUserId, Guid otherUserId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
    {
        foreach (var message in _messages)
        {
            var isUnreadFromOtherParty =
                message.SenderUserId == otherUserId && message.RecipientUserId == readerUserId && !_readAtUtcByMessageId.ContainsKey(message.Id);
            if (isUnreadFromOtherParty)
            {
                _readAtUtcByMessageId[message.Id] = readAtUtc;
            }
        }

        return Task.CompletedTask;
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

    public Task<IReadOnlyList<ChatMessage>> GetGroupConversationAsync(Guid groupId, Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ChatMessage>>(
            _messages
                .Where(message => message.GroupId == groupId && (message.SenderUserId == userId || message.RecipientUserId == userId))
                .OrderBy(message => message.SentAtUtc)
                .ToList());

    public Task DeleteAsync(Guid messageId, CancellationToken cancellationToken)
    {
        _messages.RemoveAll(message => message.Id == messageId);
        return Task.CompletedTask;
    }

    public Task DeleteGroupMessageAsync(Guid groupMessageId, CancellationToken cancellationToken)
    {
        _messages.RemoveAll(message => message.GroupMessageId == groupMessageId);
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
}