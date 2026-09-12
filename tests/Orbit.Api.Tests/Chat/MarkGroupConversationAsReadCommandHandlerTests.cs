using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Chat;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Chat.Groups.MarkGroupConversationAsRead;
using Xunit;

namespace Orbit.Api.Tests.Chat;

/// <summary>
/// The group's "read up to" - the same rules as the one-to-one conversation's
/// (MarkConversationAsReadCommandHandlerTests), applied to the reader's own copies of the group.
/// </summary>
public sealed class MarkGroupConversationAsReadCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_marks_only_up_to_the_newest_message_seen()
    {
        var context = new GroupReadContext();
        var seen = await context.ReceiveAsync(minutesAgo: 2);
        var notYetSeen = await context.ReceiveAsync(minutesAgo: 1);

        await context.MarkReadAsync(seen.SentAtUtc);

        Assert.True(await context.IsReadByReaderAsync(seen));
        Assert.False(await context.IsReadByReaderAsync(notYetSeen));
    }

    /// <summary>Installed phone builds call without one, and still mark the whole group - see the one-to-one test.</summary>
    [Fact]
    public async Task HandleAsync_without_a_read_up_to_marks_everything()
    {
        var context = new GroupReadContext();
        var older = await context.ReceiveAsync(minutesAgo: 2);
        var newer = await context.ReceiveAsync(minutesAgo: 1);

        await context.MarkReadAsync(readUpToUtc: null);

        Assert.True(await context.IsReadByReaderAsync(older));
        Assert.True(await context.IsReadByReaderAsync(newer));
    }

    /// <summary>
    /// The rest of the group hears about a narrower read only when it changed a copy - a group is where
    /// an empty announcement would be answered by everybody at once.
    /// </summary>
    [Fact]
    public async Task HandleAsync_announces_only_the_reads_that_changed_something()
    {
        var context = new GroupReadContext();
        var first = await context.ReceiveAsync(minutesAgo: 2);
        var second = await context.ReceiveAsync(minutesAgo: 1);

        await context.MarkReadAsync(first.SentAtUtc.AddTicks(-1));
        await context.MarkReadAsync(first.SentAtUtc);
        await context.MarkReadAsync(first.SentAtUtc);
        await context.MarkReadAsync(second.SentAtUtc);

        Assert.Equal([context.SenderId, context.SenderId], context.Announcements.ChatToldAbout);
    }

    [Fact]
    public async Task HandleAsync_refuses_somebody_outside_the_group_whatever_they_say_they_saw()
    {
        var context = new GroupReadContext();
        var message = await context.ReceiveAsync(minutesAgo: 1);

        var marked = await new MarkGroupConversationAsReadCommandHandler(
                context.Groups, context.Messages, context.Announcements)
            .HandleAsync(
                new MarkGroupConversationAsReadCommand(Guid.NewGuid(), context.GroupId, message.SentAtUtc),
                CancellationToken.None);

        Assert.False(marked);
        Assert.Empty(context.Announcements.ChatToldAbout);
    }

    private sealed class GroupReadContext
    {
        private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

        public InMemoryChatGroupRepository Groups { get; } = new();
        public InMemoryChatMessageRepository Messages { get; } = new();
        public RecordingLiveUpdatePublisher Announcements { get; } = new();
        public Guid ReaderId { get; } = Guid.NewGuid();
        public Guid SenderId { get; } = Guid.NewGuid();
        public Guid GroupId { get; }

        public GroupReadContext()
        {
            var group = ChatGroup.Create(ReaderId, "Weekend trip");
            group.AddMember(ReaderId, SenderId);
            GroupId = group.Id;
            Groups.AddAsync(group, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task<ChatMessage> ReceiveAsync(int minutesAgo)
        {
            var message = ChatMessage.CreateForGroup(
                GroupId, Guid.NewGuid(), SenderId, ReaderId, "ciphertext", "nonce", _now.AddMinutes(-minutesAgo));
            await Messages.AddAsync(message, CancellationToken.None);
            return message;
        }

        public Task MarkReadAsync(DateTimeOffset? readUpToUtc)
            => new MarkGroupConversationAsReadCommandHandler(Groups, Messages, Announcements)
                .HandleAsync(new MarkGroupConversationAsReadCommand(ReaderId, GroupId, readUpToUtc), CancellationToken.None);

        public async Task<bool> IsReadByReaderAsync(ChatMessage message)
        {
            var receipts = await Messages.GetGroupReceiptsAsync([message.GroupMessageId!.Value], CancellationToken.None);
            return receipts[message.GroupMessageId.Value].Single(receipt => receipt.RecipientUserId == ReaderId).ReadAtUtc
                is not null;
        }
    }
}
