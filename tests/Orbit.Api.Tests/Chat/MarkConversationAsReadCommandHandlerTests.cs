using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat;
using Orbit.Core.Chat.MarkConversationAsRead;
using Orbit.Core.LiveUpdates;
using Xunit;

namespace Orbit.Api.Tests.Chat;

public sealed class MarkConversationAsReadCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_marks_the_other_partys_messages_as_read()
    {
        var repository = new InMemoryChatMessageRepository();
        var readerId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        var message = ChatMessage.Create(otherPartyId, readerId, "ciphertext", "nonce");
        await repository.AddAsync(message, CancellationToken.None);
        var handler = new MarkConversationAsReadCommandHandler(repository, new SilentLiveUpdatePublisher());

        var result = await handler.HandleAsync(new MarkConversationAsReadCommand(readerId, otherPartyId), CancellationToken.None);

        Assert.True(result);
        var readUpToUtc = await repository.GetReadUpToUtcAsync(otherPartyId, readerId, CancellationToken.None);
        Assert.Equal(message.SentAtUtc, readUpToUtc);
    }

    [Fact]
    public async Task HandleAsync_does_not_mark_the_readers_own_messages_as_read()
    {
        var repository = new InMemoryChatMessageRepository();
        var readerId = Guid.NewGuid();
        var otherPartyId = Guid.NewGuid();
        var ownMessage = ChatMessage.Create(readerId, otherPartyId, "ciphertext", "nonce");
        await repository.AddAsync(ownMessage, CancellationToken.None);
        var handler = new MarkConversationAsReadCommandHandler(repository, new SilentLiveUpdatePublisher());

        await handler.HandleAsync(new MarkConversationAsReadCommand(readerId, otherPartyId), CancellationToken.None);

        var readUpToUtc = await repository.GetReadUpToUtcAsync(readerId, otherPartyId, CancellationToken.None);
        Assert.Null(readUpToUtc);
    }

    [Fact]
    public async Task HandleAsync_succeeds_even_when_there_is_nothing_to_mark()
    {
        var handler = new MarkConversationAsReadCommandHandler(new InMemoryChatMessageRepository(), new SilentLiveUpdatePublisher());

        var result = await handler.HandleAsync(
            new MarkConversationAsReadCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result);
    }

    /// <summary>
    /// The newest message the reader has seen, and nothing after it: a message that arrived below the
    /// bottom of the screen has not been read just because the conversation was open.
    /// </summary>
    [Fact]
    public async Task HandleAsync_marks_only_up_to_the_newest_message_seen()
    {
        var context = new ReadUpToContext();
        var seen = await context.ReceiveAsync(minutesAgo: 2);
        await context.ReceiveAsync(minutesAgo: 1);

        await context.MarkReadAsync(seen.SentAtUtc);

        Assert.Equal(seen.SentAtUtc, await context.ReadUpToUtcAsync());
        Assert.Equal(1, await context.UnreadCountAsync());
    }

    /// <summary>
    /// Installed phone builds call the route without a "read up to" until a rebuilt APK replaces them,
    /// and they must go on marking everything, the way they always have.
    /// </summary>
    [Fact]
    public async Task HandleAsync_without_a_read_up_to_marks_everything()
    {
        var context = new ReadUpToContext();
        await context.ReceiveAsync(minutesAgo: 2);
        var newest = await context.ReceiveAsync(minutesAgo: 1);

        await context.MarkReadAsync(readUpToUtc: null);

        Assert.Equal(newest.SentAtUtc, await context.ReadUpToUtcAsync());
        Assert.Equal(0, await context.UnreadCountAsync());
    }

    [Fact]
    public async Task HandleAsync_with_a_read_up_to_older_than_every_message_marks_nothing()
    {
        var context = new ReadUpToContext();
        var only = await context.ReceiveAsync(minutesAgo: 1);

        await context.MarkReadAsync(only.SentAtUtc.AddTicks(-1));

        Assert.Null(await context.ReadUpToUtcAsync());
        Assert.Equal(1, await context.UnreadCountAsync());
    }

    /// <summary>
    /// A narrower read is still announced only when it changed a row - the terminating rule of
    /// LiveUpdateAnnouncementTests holds for every "read up to", not just for "everything".
    /// </summary>
    [Fact]
    public async Task HandleAsync_announces_only_the_reads_that_changed_something()
    {
        var context = new ReadUpToContext();
        var first = await context.ReceiveAsync(minutesAgo: 2);
        var second = await context.ReceiveAsync(minutesAgo: 1);

        await context.MarkReadAsync(first.SentAtUtc.AddTicks(-1));
        await context.MarkReadAsync(first.SentAtUtc);
        await context.MarkReadAsync(first.SentAtUtc);
        await context.MarkReadAsync(second.SentAtUtc);

        Assert.Equal([context.OtherPartyId, context.OtherPartyId], context.Announcements.ChatToldAbout);
    }

    private sealed class ReadUpToContext
    {
        private readonly InMemoryChatMessageRepository _repository = new();
        private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

        public RecordingLiveUpdatePublisher Announcements { get; } = new();
        public Guid ReaderId { get; } = Guid.NewGuid();
        public Guid OtherPartyId { get; } = Guid.NewGuid();

        public async Task<ChatMessage> ReceiveAsync(int minutesAgo)
        {
            var message = ChatMessage.FromPersistence(
                Guid.NewGuid(), OtherPartyId, ReaderId, "ciphertext", "nonce", _now.AddMinutes(-minutesAgo),
                isEdited: false, editedAtUtc: null);
            await _repository.AddAsync(message, CancellationToken.None);
            return message;
        }

        public Task MarkReadAsync(DateTimeOffset? readUpToUtc)
            => new MarkConversationAsReadCommandHandler(_repository, Announcements)
                .HandleAsync(new MarkConversationAsReadCommand(ReaderId, OtherPartyId, readUpToUtc), CancellationToken.None);

        public Task<DateTimeOffset?> ReadUpToUtcAsync()
            => _repository.GetReadUpToUtcAsync(OtherPartyId, ReaderId, CancellationToken.None);

        public async Task<int> UnreadCountAsync()
            => (await _repository.GetUnreadCountsBySenderAsync(ReaderId, CancellationToken.None))
                .GetValueOrDefault(OtherPartyId);
    }
}
